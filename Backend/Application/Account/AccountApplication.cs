using PhotonBypass.Application.Account.Model;
using PhotonBypass.Infra;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Account;

internal class AccountApplication(AccountLocalRepository local_repository) : IAccountApplication
{
    private readonly AccountLocalRepository local_repository = local_repository;

    public async Task<ApiResult<UserModel>> GetUser(string username)
    {
        var user = await local_repository.GetUser(username) ??
            throw new UserException("کاربر پیدا نشد!");

        var target_area = (await local_repository.GetTargetArea(user.Id))
            .Select(user => new TargetModel
            {
                Username = user.Username,
                Fullname = user.Fullname,
                Email = user.Email,
            })
            .ToDictionary(k => k.Username);

        return ApiResult<UserModel>.Success(new UserModel
        {
            Username = user.Username,
            Balance = user.Balance,
            Email = user.Email,
            Fullname = user.Fullname,
            Picture = user.Picture,
            TargetArea = target_area,
        });
    }

    public async Task<ApiResult<FullUserModel>> GetFullInfo(string target)
    {
        var user = await local_repository.GetUser(target) ??
            throw new UserException("کاربر پیدا نشد!");

        return ApiResult<FullUserModel>.Success(new FullUserModel
        {
            Username = user.Username,
            Firstname = user.Name,
            Lastname = user.Surname,
            Email = user.Email,
            EmailValid = user.EmailValid,
            Mobile = user.Mobile,
            MobileValid = user.MobileValid,
        });
    }

    public Task<ApiResult> EditUser(string target, EditUserContext model)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult> ChangePassword(string target, ChangePasswordContext context)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult<IList<HistoryModel>>> GetHistory(HistoryContext context)
    {
        throw new NotImplementedException();
    }
}
