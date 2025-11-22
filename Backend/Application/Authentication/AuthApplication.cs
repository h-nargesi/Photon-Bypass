using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Services;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;
using System.Text.RegularExpressions;
using PhotonBypass.Domain.Account.Business;

namespace PhotonBypass.Application.Authentication;

partial class AuthApplication(
    IAccountRepository AccountRepo,
    Lazy<IResetPassRepository> ResetPassRepo,
    Lazy<IPermanentUsersRepository> UserRepo,
    Lazy<IStaticRepository> StaticRepo,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IAccountRadiusSyncService> RadiusSrv,
    Lazy<IEmailService> EmailSrv,
    Lazy<ISocialMediaService> SocialMediaSrv,
    Lazy<IHistoryRepository> HistoryRepo)
    : IAuthApplication
{
    public async Task<ApiResult<UserModel>> CheckUserPassword(string username, string password)
    {
        var account = await AccountRepo.GetAccount(username) ??
            await CopyFromPermanentUser(username, password);

        if (account == null || account.Password != HashHandler.HashPassword(password))
        {
            if (account == null)
            {
                return new ApiResult<UserModel>
                {
                    Code = 401,
                    Data = null,
                };
            }
            
            _ = SocialMediaSrv.Value.InvalidPasswordAlert(account.Username);
            _ = HistoryRepo.Value.Save(new HistoryEntity
            {
                Target = account.Username,
                EventTime = DateTime.Now,
                Title = "امنیت",
                Description = "تلاش برای ورود با کلمه عبور اشتباه",
            });

            Log.Warning("Invalid password for {0}", account.Username);

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

        Log.Information("[user: {0}] User loged in", account.Username);

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

        if (MobileValidator().IsMatch(email_mobile))
        {
#if !SOCIAL
            return new ApiResult
            {
                Code = 100,
                Message = "موبایل هنوز پشتیبانی نشده است!"
            };
#else
            var account = await AccountRepo.GetAccountByMobile(email_mobile);

            if (account != null)
            {
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
            }

            return ApiResult.Success("پیام به واتساپ ارسال شد.");
#endif
        }
        else if (EmailValidator().IsMatch(email_mobile))
        {
            var account = await AccountRepo.GetAccountByMobile(email_mobile);

            if (account == null) throw new UserException("کاربر یافت نشد.");
            
            var hash_code = HashHandler.GenerateHashCode(56);

            var insert_task = ResetPassRepo.Value.AddHashCode(new ResetPassEntity
            {
                AccountId = account.Id,
                ExpireDate = DateTime.Now.AddDays(1),
                HashCode = hash_code,
            });

            var email_task = EmailSrv.Value.SendResetPasswordLink(account.Fullname, email_mobile, hash_code);

            _ = HistoryRepo.Value.Save(new HistoryEntity
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

        account.Password = HashHandler.HashPassword(model.Password ?? string.Empty);

        var account_saving = AccountRepo.Save(account);

        var realm = await ServerMngSrv.Value.GetAvailableRealm();

        var accouint RadiusSrv.Value.RegisterUser(account);

        var setting_server = RadiusSrv.Value.SetRestrictedServer(user.Username, realm);

        _ = SocialMediaSrv.Value.NewUserRegistrationAlert(account);

        Task.WaitAll(account_saving, setting_server);

        Log.Information("New User Registered: ({0}, {1})", account.Username, account.Email);

        return ApiResult.Success("کاربر شما ساخته شد.");
    }

    public async Task<AccountEntity?> CopyFromPermanentUser(string username, string? password)
    {
        if (!RadiusSrv.IsValueCreated) return null;

        var account = await RadiusSrv.Value.GetUser(username);

        if (account == null) return null;

        if (password != null && account.Password != password)
        {
            return null;
        }

        await AccountRepo.Save(account);

        return account;
    }

    [GeneratedRegex(@"^\+?\d{5,16}$")]
    private static partial Regex MobileValidator();

    [GeneratedRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")]
    private static partial Regex EmailValidator();
}
