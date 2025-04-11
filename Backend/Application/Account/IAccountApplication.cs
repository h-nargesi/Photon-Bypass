using PhotonBypass.Application.Account.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Account;

public interface IAccountApplication
{
    Task<ApiResult> GetUser(string username);

    Task<ApiResult<FullUserModel>> GetFullInfo(string target);

    Task<ApiResult> EditUser(string target, EditUserContext model);

    Task<ApiResult> ChangePassword(string target, ChangePasswordContext context);

    Task<ApiResult<HistoryModel[]>> GetHistory(HistoryContext context);
}
