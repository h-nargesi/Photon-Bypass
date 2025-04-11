using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Authentication;

public interface IAuthApplication
{
    Task<ApiResult<FullUserModel>> CheckUserPassword(TokenContext context);

    Task<ApiResult> ResetPassword(ResetPasswordContext context);

    Task<ApiResult> Register(RegisterContext context);
}
