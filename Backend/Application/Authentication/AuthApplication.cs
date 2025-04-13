using System;
using System.Text.RegularExpressions;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Application.Database;
using PhotonBypass.Domain.User;
using PhotonBypass.Infra;
using PhotonBypass.Infra.Controller;
using PhotonBypass.OutSource;

namespace PhotonBypass.Application.Authentication;

partial class AuthApplication(
    AccountRepository account_repository,
    ResetPassRepository reset_pass_repository,
    // IWhatsAppHandler whatsapp_handler,
    IEmailHandler email_handler) : IAuthApplication
{
    public async Task<ApiResult<TargetModel>> CheckUserPassword(string username, string password)
    {
        password = HashHandler.HashPassword(password);

        var account = await account_repository.GetAccount(username) ??
            throw new UserException("کاربر پیدا نشد!");

        if (account.Password != password)
        {
            return new ApiResult<TargetModel>
            {
                Code = 401,
                Data = null,
            };
        }

        return new ApiResult<TargetModel>
        {
            Code = 200,
            Data = new TargetModel
            {
                Username = account.Username,
                Fullname = account.Fullname,
                Email = account.Email,
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
            var account = await account_repository.GetAccountByMobile(email_mobile);

            if (account != null)
            {
                var hash_code = HashHandler.GewnerateHashCode(56);

                await reset_pass_repository.AddHashCode(new ResetPassEntity
                {
                    AccountId = account.Id,
                    ExpireDate = DateTime.Now.AddDays(1),
                    HashCode = hash_code,
                });

                await email_handler.SendResetPasswordLink(email_mobile, hash_code);
            }

            return ApiResult.Success("ایمیل ارسال شد.");
        }

        return new ApiResult
        {
            Code = 400,
            Message = "ایمیل/موبایل نا معتبر است!"
        };
    }

    public Task<ApiResult> Register(RegisterContext context)
    {
        throw new NotImplementedException();
    }

    [GeneratedRegex(@"^\+?\d{5,16}$")]
    private static partial Regex MobileValidator();

    [GeneratedRegex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$")]
    private static partial Regex EmailValidator();
}
