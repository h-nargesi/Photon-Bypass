using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Authentication;

public interface IAuthApplication
{
    Task<ApiResult<UserModel>> CheckUserPassword(string username, string password);

    Task<ApiResult> ForgetPassword(string email_mobile);

    Task<ApiResult> ResetPassword(string code, string password);

    Task<ApiResult> Register(RegisterModel model);
}
