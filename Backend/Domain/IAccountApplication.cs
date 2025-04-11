using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Domain.Model.User;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IAccountApplication
{
    Task<ApiResult<FullUserModel>> GetFullInfo(FullInfoContext context);

    Task<ApiResult> EditUser(EditUserModel model);

    Task<ApiResult<HistoryModel[]>> GetHistory(HistoryContext context);
}
