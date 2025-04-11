using PhotonBypass.Domain.Model.Auth;
using PhotonBypass.Domain.Model.User;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IAuthApplication
{
    Task<ApiResult<FullUserModel>> CheckUserPassword(TokenContext context);
    
    Task<ApiResult> ResetPassword(ResetPasswordContext context);
}
