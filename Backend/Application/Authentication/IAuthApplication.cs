using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Authentication;

public interface IAuthApplication
{
    Task<ApiResult<TargetModel>> CheckUserPassword(string username, string password);

    Task<ApiResult> ResetPassword(string email_mobile);

    Task<ApiResult> Register(RegisterContext context);
}
