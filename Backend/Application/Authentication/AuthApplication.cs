using System.Text.RegularExpressions;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Application.Database;
using PhotonBypass.Application.Management;
using PhotonBypass.Domain.Local;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra;
using PhotonBypass.Infra.Controller;
using PhotonBypass.OutSource;

namespace PhotonBypass.Application.Authentication;

partial class AuthApplication(
    AccountRepository AccountRepo,
    Lazy<ResetPassRepository> ResetPassRepo,
    Lazy<PermenantUsersRepository> UserRepo,
    Lazy<StaticRepository> StaticRepo,
    Lazy<ServerManagementService> ServerManageSrv,
    Lazy<IRadiusDeskService> RadiusDeskSrv,
    //Lazy<IWhatsAppHandler> whatsapp_handler,
    Lazy<IEmailHandler> EmailHdl
    ) : IAuthApplication
{
    public async Task<ApiResult<UserModel>> CheckUserPassword(string username, string password)
    {
        password = HashHandler.HashPassword(password);

        var account = await AccountRepo.GetAccount(username) ??
            throw new UserException("کاربر پیدا نشد!");

        if (account.Password != password)
        {
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
            //}

            //return ApiResult.Success("پیام به واتساپ ارسال شد.");
        }
        else if (EmailValidator().IsMatch(email_mobile))
        {
            var account = await AccountRepo.GetAccountByMobile(email_mobile);

            if (account != null)
            {
                var hash_code = HashHandler.GewnerateHashCode(56);

                await ResetPassRepo.Value.AddHashCode(new ResetPassEntity
                {
                    AccountId = account.Id,
                    ExpireDate = DateTime.Now.AddDays(1),
                    HashCode = hash_code,
                });

                await EmailHdl.Value.SendResetPasswordLink(email_mobile, hash_code);
            }

            return ApiResult.Success("ایمیل ارسال شد.");
        }

        throw new UserException("ایمیل/موبایل نا معتبر است!");
    }

    public async Task<ApiResult> Register(RegisterContext context)
    {
        var currentRealm = await ServerManageSrv.Value.GetCurrentRealm(StaticRepo.Value.WebCloudID);

        if (await UserRepo.Value.CheckUsername(context.Username + currentRealm.Suffix))
        {
            throw new UserException("این نام کاربری قبلا استفاده شده است!");
        }

        var user_saving = RadiusDeskSrv.Value.SavePermenentUser(new PermenantUserEntity
        {
            Username = context.Username ?? string.Empty,
            CloudId = StaticRepo.Value.WebCloudID,
            Email = context.Email,
            Phone = context.Mobile,
            Name = context.Firstname,
            Surname = context.Lastname,
            Realm = currentRealm.Name,
            RealmId = currentRealm.Id,
            Profile = StaticRepo.Value.DefaultProfile.Name,
            ProfileId = StaticRepo.Value.DefaultProfile.Id,
            Active = false,
        });

        var account_saving = AccountRepo.Save(new AccountEntity
        {
            Username = context.Username ?? string.Empty,
            CloudId = StaticRepo.Value.WebCloudID,
            Email = context.Email,
            EmailValid = false,
            Mobile = context.Mobile,
            MobileValid = false,
            Name = context.Firstname,
            Surname = context.Lastname,
            Password = HashHandler.HashPassword(context.Password ?? string.Empty),
        });

        Task.WaitAll(account_saving, user_saving);

        return ApiResult.Success("کاربر شما ساخته شد.");
    }

    [GeneratedRegex(@"^\+?\d{5,16}$")]
    private static partial Regex MobileValidator();

    [GeneratedRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")]
    private static partial Regex EmailValidator();
}
