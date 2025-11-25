using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Authentication;

public interface IAuthApplication
{
    Task<ApiResult<UserModel>> CheckUserPassword(string username, string password);

    Task<ApiResult> ResetPassword(string email_mobile);

    Task<ApiResult> Register(RegisterModel model);
    
    Task<AccountEntity?> CopyFromPermanentUser(string username, string? password);
}
