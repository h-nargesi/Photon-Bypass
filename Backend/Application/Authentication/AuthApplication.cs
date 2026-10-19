using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Authentication;

class AuthApplication(
    IAccountRepository account_repo,
    IHistoryRepository history_repo,
    Lazy<IResetPassRepository> reset_pass_repo,
    ISocialMediaService social_media_srv,
    Lazy<IAccountApplication> account_app,
    Lazy<IEmailService> email_srv,
    IMemoryCache reset_limit_cache,
    IOptions<ManagementOptions> options)
    : IAuthApplication
{
    /// <summary>Reset-code requests allowed per identifier inside the window.</summary>
    private const int MaxResetRequestsPerWindow = 3;

    private static readonly TimeSpan ResetRateWindowLength = TimeSpan.FromHours(1);

    private static readonly object ResetRateLimitLock = new();

    private IAccountRepository AccountRepo { get; } = account_repo;
    private IHistoryRepository HistoryRepo { get; } = history_repo;
    private Lazy<IResetPassRepository> ResetPassRepo { get; } = reset_pass_repo;
    private ISocialMediaService SocialMediaSrv { get; } = social_media_srv;
    private Lazy<IAccountApplication> AccountApp { get; } = account_app;
    private Lazy<IEmailService> EmailSrv { get; } = email_srv;
    private IMemoryCache ResetLimitCache { get; } = reset_limit_cache;
    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<ApiResult<UserModel>> CheckUserPassword(string username, string password)
    {
        var account = await AccountRepo.GetAccount(username);

        // portal-accessible: Deactivated is blocked with 401; VpnSuspended
        // still logs in (docs/20 D13)
        if (account == null || account.Status == AccountStatus.Deactivated || !PasswordHasher.Verify(password, account.Password))
        {
            if (account == null)
            {
                _ = PasswordHasher.Hash(password);

                return new ApiResult<UserModel>
                {
                    Code = 401,
                    Data = null,
                };
            }

            await SocialMediaSrv.InvalidPasswordAlert(account.Username);

            if (account.Status != AccountStatus.Deactivated)
            {
                await HistoryRepo.Save(new HistoryEntity
                {
                    Target = account.Id,
                    Category = EventCategory.Security,
                    Type = EventType.Critical,
                    Title = "امنیت",
                    Description = "تلاش برای ورود با کلمه عبور اشتباه",
                });
            }

            Log.Warning("Invalid password for {0}, status={1}", account.Username, account.Status);

            return new ApiResult<UserModel>
            {
                Code = 401,
                Data = null,
            };
        }

        var target_list = account.Role == AccountRole.Admin ?
            await AccountRepo.GetAllActive() :
            await AccountRepo.GetActiveTargetArea(account.Id);

        var target_area = target_list
            .Select(user => new TargetModel
            {
                Username = user.Username,
                Fullname = user.Fullname,
                Email = user.Email,
            })
            .ToDictionary(k => k.Username);

        Log.Information("[user: {0}] User logged in", account.Username);

        return new ApiResult<UserModel>
        {
            Code = 200,
            Data = new UserModel
            {
                Username = account.Username,
                Fullname = account.Fullname,
                Email = account.Email,
                Role = account.Role,
                AllowSelfService = account.UserType.HasFlag(UserTypes.AllowSelfService),
                TargetArea = target_area,
            },
            Message = "وارد شدید.",
        };
    }

    public async Task<ApiResult> ForgetPassword(string email_mobile)
    {
        email_mobile = email_mobile.Trim();

        if (AccountBusiness.MobileNumberPattern().IsMatch(email_mobile))
        {
            // the limiter runs before the lookup (also for unknown accounts)
            // so it cannot be used as an enumeration oracle; the response
            // stays neutral when the limit is exhausted
            if (!TryConsumeResetRequest(email_mobile))
            {
                return ApiResult.Success("اگر این حساب ثبت شده باشد، پیام بازیابی ارسال شد.");
            }

#if !SOCIAL
            return new ApiResult
            {
                Code = 100,
                Message = "موبایل هنوز پشتیبانی نشده است!"
            };
#else
            var account = await AccountRepo.GetAccountByMobile(email_mobile);

            if (account is { Status: not AccountStatus.Deactivated })
            {
                var hash_code = HashHandler.GenerateHashCode(56);

                await ResetPassRepo.Value.ExpireActiveCodes(account.Id);

                await ResetPassRepo.Value.AddHashCode(new ResetPassEntity
                {
                    AccountId = account.Id,
                    ExpireDate = DateTime.Now.AddDays(1),
                    HashCode = hash_code,
                });

                await SocialMediaSrv.SendResetPasswordLink(email_mobile, hash_code);

                await HistoryRepo.Save(new HistoryEntity
                {
                    Target = account.Id,
                    Category = EventCategory.Security,
                    Type = EventType.Information,
                    Title = "امنیت",
                    Description = "درخواست تغییر کلمه عبور (موبایل)",
                });

                Log.Information("Reset-Password message has been sent: account-id={0}", account.Id);
            }

            return ApiResult.Success("اگر این حساب ثبت شده باشد، پیام بازیابی ارسال شد.");
#endif
        }
        else if (AccountBusiness.EmailPattern().IsMatch(email_mobile))
        {
            if (!TryConsumeResetRequest(email_mobile))
            {
                return ApiResult.Success("اگر این حساب ثبت شده باشد، ایمیل بازیابی ارسال شد.");
            }

            var account = await AccountRepo.GetAccountByEmail(email_mobile);

            if (account is { Status: not AccountStatus.Deactivated })
            {
                var hash_code = HashHandler.GenerateHashCode(56, true);

                // the inserts share the scoped SqlConnection and run one after
                // the other; only then is the email sent, so a stored code is
                // never announced before it exists
                await ResetPassRepo.Value.ExpireActiveCodes(account.Id);

                await ResetPassRepo.Value.AddHashCode(new ResetPassEntity
                {
                    AccountId = account.Id,
                    ExpireDate = DateTime.Now.AddDays(1),
                    HashCode = hash_code,
                });

                await HistoryRepo.Save(new HistoryEntity
                {
                    Target = account.Id,
                    Category = EventCategory.Security,
                    Type = EventType.Information,
                    Title = "امنیت",
                    Description = "درخواست تغییر کلمه عبور (ایمیل)",
                });

                try
                {
                    await EmailSrv.Value.SendResetPasswordLink(account.Fullname, email_mobile, hash_code);
                }
                catch (Exception error)
                {
                    // the code stays stored: the user may simply retry later
                    Log.Error(error, "Reset-Password email failed: account-id={0}", account.Id);

                    throw new UserException("ارسال ایمیل ناموفق بود؛ لطفاً بعداً تلاش کنید",
                        $"Reset-password email failed: account-id={account.Id}");
                }

                Log.Information("Reset-Password email has been sent: account-id={0}", account.Id);
            }

            return ApiResult.Success("اگر این حساب ثبت شده باشد، ایمیل بازیابی ارسال شد.");
        }

        throw new UserException("ایمیل/موبایل نا معتبر است!",
            $"Invalid Email/Mobile: {email_mobile}");
    }

    /// <summary>
    /// Sliding-window limiter (3 requests per identifier per hour) backed by
    /// the process-wide <see cref="IMemoryCache"/>; every well-formed request
    /// consumes a slot whether or not the account exists.
    /// </summary>
    private bool TryConsumeResetRequest(string email_mobile)
    {
        var key = $"reset-limit|{email_mobile.ToLowerInvariant()}";

        lock (ResetRateLimitLock)
        {
            var now = DateTime.Now;

            if (!ResetLimitCache.TryGetValue(key, out ResetRateWindow? window) || window is null)
            {
                window = new ResetRateWindow();
            }

            while (window.Requests.Count > 0 && window.Requests.Peek() <= now - ResetRateWindowLength)
            {
                window.Requests.Dequeue();
            }

            if (window.Requests.Count >= MaxResetRequestsPerWindow)
            {
                return false;
            }

            window.Requests.Enqueue(now);

            ResetLimitCache.Set(key, window, now + ResetRateWindowLength);

            return true;
        }
    }

    private sealed class ResetRateWindow
    {
        public Queue<DateTime> Requests { get; } = new();
    }

    public async Task<ApiResult> ResetPassword(string code, string password)
    {
        var reset_pass = await ResetPassRepo.Value.GetAccount(code);

        if (reset_pass == null || reset_pass.ExpireDate <= DateTime.Now)
        {
            throw new UserException("این کد منقضی شده است! دوباره تلاش کنید.");
        }
        
        var account = await AccountRepo.GetActiveAccount(reset_pass.AccountId);

        return await AccountApp.Value.ChangePassword(account, password);
    }

    public async Task<ApiResult> Register(RegisterModel model)
    {
        if (!Options.Value.AllowRegistration)
        {
            throw new UserException("ثبت‌نام عمومی فعال نیست!",
                "Public registration is disabled (ManagementOptions:AllowRegistration).", 403);
        }

        var account = AccountBusiness.CreateFromModel(model);

        AccountBusiness.ValidatePassword(model.Password);

        var duplicated = await AccountRepo.CheckUniqueData(account.Username, account.Email, account.Mobile);

        if ((duplicated & 1) == 1)
        {
            throw new UserException("این نام کاربری قبلا استفاده شده است!");
        }

        if ((duplicated & 2) == 2)
        {
            throw new UserException("این ایمیل قبلا استفاده شده است!");
        }

        if ((duplicated & 4) == 4)
        {
            throw new UserException("این  شماره همراه قبلا استفاده شده است!");
        }

        account.VpnPassword = HashHandler.GenerateHashCode();
        account.Password = PasswordHasher.Hash(model.Password ?? string.Empty);
        account.UserType |= UserTypes.AllowSelfService;

        await AccountRepo.Save(account);

        await SocialMediaSrv.NewUserRegistrationAlert(account);

        if (account.Email != null)
        {
            // the address was just submitted in this request — the very same
            // channel ForgetPassword mails — so it needs no validation stamp.
            // Best-effort: registration must never fail because of the email.
            try
            {
                await EmailSrv.Value.SendWelcomeEmail(account.Fullname, account.Email, account.Username);
            }
            catch (Exception error)
            {
                Log.Error(error, "Welcome email failed: account-id={0}, user={1}", account.Id, account.Username);
            }
        }

        Log.Information("New User Registered: ({0}, {1})", account.Username, account.Email);

        return ApiResult.Success("کاربر شما ساخته شد.");
    }
}