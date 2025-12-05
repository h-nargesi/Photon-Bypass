using PhotonBypass.Application.Account.Model;
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
    Lazy<IEmailService> email_srv)
    : IAuthApplication
{
    private IAccountRepository AccountRepo { get; } = account_repo;
    private IHistoryRepository HistoryRepo { get; } = history_repo;
    private Lazy<IResetPassRepository> ResetPassRepo { get; } = reset_pass_repo;
    private ISocialMediaService SocialMediaSrv { get; } = social_media_srv;
    private Lazy<IEmailService> EmailSrv { get; } = email_srv;

    public async Task<ApiResult<UserModel>> CheckUserPassword(string username, string password)
    {
        var account = await AccountRepo.GetAccount(username);

        if (account is not { Active: true } || account.Password != HashHandler.HashPassword(password))
        {
            if (account == null)
            {
                return new ApiResult<UserModel>
                {
                    Code = 401,
                    Data = null,
                };
            }

            _ = SocialMediaSrv.InvalidPasswordAlert(account.Username);

            if (account.Active)
            {
                _ = HistoryRepo.Save(new HistoryEntity
                {
                    Target = account.Username,
                    EventTime = DateTime.Now,
                    Title = "امنیت",
                    Description = "تلاش برای ورود با کلمه عبور اشتباه",
                });
            }

            Log.Warning("Invalid password for {0}, active={1}", account.Username, account.Active);

            return new ApiResult<UserModel>
            {
                Code = 401,
                Data = null,
            };
        }

        var target_area = (await AccountRepo.GetTargetArea(account.Id))
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
                TargetArea = target_area,
            },
        };
    }

    public async Task<ApiResult> ResetPassword(string email_mobile)
    {
        email_mobile = email_mobile.Trim();

        if (AccountBusiness.MobileNumberPattern().IsMatch(email_mobile))
        {
#if !SOCIAL
            return new ApiResult
            {
                Code = 100,
                Message = "موبایل هنوز پشتیبانی نشده است!"
            };
#else
            var account = (await AccountRepo.GetAccountByMobile(email_mobile)) ?? 
                throw new UserException("کاربر یافت نشد.");
                
            if (!account.Active)
            {
                throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
            }
            
            var hash_code = HashHandler.GenerateHashCode(56);

            await ResetPassRepo.Value.AddHashCode(new ResetPassEntity
            {
                AccountId = account.Id,
                ExpireDate = DateTime.Now.AddDays(1),
                HashCode = hash_code,
            });

            await SocialMediaSrv.Value.SendResetPasswordLink(email_mobile, hash_code);

            // TODO: History record

            Log.Verbose("Reset-Password message has been sent: {0}", email_mobile);

            return ApiResult.Success("پیام به واتساپ ارسال شد.");
#endif
        }
        else if (AccountBusiness.EmailPattern().IsMatch(email_mobile))
        {
            var account = (await AccountRepo.GetAccountByMobile(email_mobile)) ??
                          throw new UserException("کاربر یافت نشد.");

            if (!account.Active)
            {
                throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
            }

            var hash_code = HashHandler.GenerateHashCode(56, true);

            var insert_task = ResetPassRepo.Value.AddHashCode(new ResetPassEntity
            {
                AccountId = account.Id,
                ExpireDate = DateTime.Now.AddDays(1),
                HashCode = hash_code,
            });

            var email_task = EmailSrv.Value.SendResetPasswordLink(account.Fullname, email_mobile, hash_code);

            _ = HistoryRepo.Save(new HistoryEntity
            {
                Target = account.Username,
                EventTime = DateTime.Now,
                Title = "امنیت",
                Description = "درخواست تغییر کلمه عبور (ایمیل)",
            });

            Log.Verbose("Reset-Password email has been sent: {0}", email_mobile);

            Task.WaitAll(insert_task, email_task);

            return ApiResult.Success("ایمیل ارسال شد.");
        }

        throw new UserException("ایمیل/موبایل نا معتبر است!",
            $"Invalid Email/Mobile: {email_mobile}");
    }

    public async Task<ApiResult> Register(RegisterModel model)
    {
        var account = AccountBusiness.CreateFromModel(model);

        if (await AccountRepo.CheckUsername(account.Username))
        {
            throw new UserException("این نام کاربری قبلا استفاده شده است!");
        }

        account.VpnPassword = account.Password = HashHandler.HashPassword(model.Password ?? string.Empty);

        await AccountRepo.Save(account);

        _ = SocialMediaSrv.NewUserRegistrationAlert(account);

        Log.Information("New User Registered: ({0}, {1})", account.Username, account.Email);

        return ApiResult.Success("کاربر شما ساخته شد.");
    }
}