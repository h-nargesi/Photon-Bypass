using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Account;

public interface IAccountApplication
{
    Task<ApiResult<UserModel>> GetUser(string username);

    Task<ApiResult<FullUserModel>> GetFullInfo(string target);

    Task<ApiResult> EditUser(string target, EditUserModel context);

    Task<ApiResult> ChangePassword(string target, string token, string password);

    Task<ApiResult<IList<HistoryModel>>> GetHistory(string target, DateTime? from, DateTime? to);
}
