using PhotonBypass.Application.Account.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Account;

public interface IAccountApplication
{
    Task<ApiResult<UserModel>> GetUser(string username);

    Task<ApiResult<FullUserModel>> GetFullInfo(string target);

    Task<ApiResult> EditUser(string target, EditUserContext context);

    Task<ApiResult> ChangePassword(string target, ChangePasswordContext context);

    Task<ApiResult<IList<HistoryModel>>> GetHistory(HistoryContext context);
}
