using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Authentication;

public interface IAuthApplication
{
    Task<ApiResult<UserModel>> CheckUserPassword(string username, string password);

    Task<ApiResult> ResetPassword(string email_mobile);

    Task<ApiResult> Register(RegisterContext context);
    
    Task<AccountEntity?> CopyFromPermanentUser(string username, string? password);
}
