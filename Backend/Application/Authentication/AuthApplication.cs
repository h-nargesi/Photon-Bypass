using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;
using System.Text.RegularExpressions;

namespace PhotonBypass.Application.Authentication;

partial class AuthApplication(
    IAccountRepository AccountRepo,
    Lazy<IResetPassRepository> ResetPassRepo,
    Lazy<IPermanentUsersRepository> UserRepo,
    Lazy<IStaticRepository> StaticRepo,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IRadiusService> RadiusSrv,
    //Lazy<IWhatsAppHandler> whatsapp_handler,
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
            if (account != null)
            {
                _ = SocialMediaSrv.Value.InvalidPasswordAlert(account.Username);
                _ = HistoryRepo.Value.Save(new HistoryEntity
                {
                    Target = account.Username,
                    EventTime = DateTime.Now,
                    Title = "امنیت",
                    Description = "تلاش برای ورود با کلمه عبور اشتباه",
                });

                Log.Warning("Invlid password for {0}", account.Username);
            }

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
            return new ApiResult
            {
                Code = 100,
                Message = "موبایل هنوز پشتیبانی نشده است!"
            };

            //var account = await account_repository.GetAccountByMobile(email_mobile);

            //if (account != null)
            //{
            //    var hash_code = HashHandler.GewnerateHashCode(56);

            //    await reset_pass_repository.AddHashCode(new ResetPassEntity
            //    {
            //        AccountId = account.Id,
            //        ExpireDate = DateTime.Now.AddDays(1),
            //        HashCode = hash_code,
            //    });

            //    await whatsapp_handler.SendResetPasswordLink(email_mobile, hash_code);

                // TODO: History record

            //   Log.Verbose("Reset-Password message has been sent: {0}", email_mobile);
            //}

            //return ApiResult.Success("پیام به واتساپ ارسال شد.");
        }
        else if (EmailValidator().IsMatch(email_mobile))
        {
            var account = await AccountRepo.GetAccountByMobile(email_mobile);

            if (account != null)
            {
                var hash_code = HashHandler.GewnerateHashCode(56);

                var insertTask = ResetPassRepo.Value.AddHashCode(new ResetPassEntity
                {
                    AccountId = account.Id,
                    ExpireDate = DateTime.Now.AddDays(1),
                    HashCode = hash_code,
                });

                var emailTask = EmailSrv.Value.SendResetPasswordLink(email_mobile, hash_code);

                _ = HistoryRepo.Value.Save(new HistoryEntity
                {
                    Target = account.Username,
                    EventTime = DateTime.Now,
                    Title = "امنیت",
                    Description = "درخواست تغییر کلمه عبور (ایمیل)",
                });

                Log.Verbose("Reset-Password email has been sent: {0}", email_mobile);

                Task.WaitAll(insertTask, emailTask);
            }

            return ApiResult.Success("ایمیل ارسال شد.");
        }

        throw new UserException("ایمیل/موبایل نا معتبر است!",
            $"Invalid Email/Mobile: {email_mobile}");
    }

    public async Task<ApiResult> Register(RegisterContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Email) && string.IsNullOrWhiteSpace(context.Mobile))
        {
            throw new UserException("حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        var realm = await ServerMngSrv.Value.GetAvalableRealm(StaticRepo.Value.WebCloudID);

        if (await UserRepo.Value.CheckUsername(context.Username + realm.Suffix))
        {
            throw new UserException("این نام کاربری قبلا استفاده شده است!");
        }

        context.Password ??= string.Empty;

        var user = new PermanentUserEntity
        {
            Username = context.Username ?? string.Empty,
            CloudId = StaticRepo.Value.WebCloudID,
            Email = context.Email,
            Phone = context.Mobile,
            Name = context.Firstname,
            Surname = context.Lastname,
            Realm = realm.Name,
            RealmId = realm.Id,
            Profile = StaticRepo.Value.DefaultProfile.Name,
            ProfileId = StaticRepo.Value.DefaultProfile.Id,
            Active = false,
        };

        await RadiusSrv.Value.RegisterPermenentUser(user, context.Password);

        if (user.Id < 1)
        {
            return ApiResult.Success("کاربر شما ساخته شد.");
        }

        var setting_server = RadiusSrv.Value.SetRestrictedServer(user.Username, realm.RestrictedServerIP);

        var account = new AccountEntity
        {
            CloudId = StaticRepo.Value.WebCloudID,
            PermanentUserId = user.Id,
            Username = user.Username,
            Email = context.Email,
            EmailValid = false,
            Mobile = context.Mobile,
            MobileValid = false,
            Name = context.Firstname,
            Surname = context.Lastname,
            Password = HashHandler.HashPassword(context.Password),
        };

        var account_saving = AccountRepo.Save(account);

        _ = SocialMediaSrv.Value.NewUserRegistrationAlert(account);

        Task.WaitAll(account_saving, setting_server);

        Log.Information("New User Registred: ({0}, {1})", account.Username, account.Email);

        return ApiResult.Success("کاربر شما ساخته شد.");
    }

    private async Task<AccountEntity?> CopyFromPermanentUser(string username, string password)
    {
        var user = await UserRepo.Value.GetUser(username);

        if (user == null) return null;

        var pass = await RadiusSrv.Value.GetOvpnPassword(user.Id);

        if (pass != password) return null;

        var account = new AccountEntity
        {
            PermanentUserId = user.Id,
            Username = username,
            Password = password,
            Active = true,
            CloudId = user.CloudId,
            Email = user.Email,
            EmailValid = true,
            Mobile = user.Phone,
            MobileValid = true,
            Name = user.Name,
            Surname = user.Surname,
        };

        await AccountRepo.Save(account);

        return account;
    }

    [GeneratedRegex(@"^\+?\d{5,16}$")]
    private static partial Regex MobileValidator();

    [GeneratedRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")]
    private static partial Regex EmailValidator();
}
