using PhotonBypass.Application.Account.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Account;

internal class AccountApplication : IAccountApplication
{
    public Task<ApiResult> GetUser(string username)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult<FullUserModel>> GetFullInfo(string target)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult> EditUser(string target, EditUserContext model)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult> ChangePassword(string target, ChangePasswordContext context)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult<HistoryModel[]>> GetHistory(HistoryContext context)
    {
        throw new NotImplementedException();
    }
}
