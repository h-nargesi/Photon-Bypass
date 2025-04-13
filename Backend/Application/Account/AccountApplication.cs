using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Database;
using PhotonBypass.Domain.User;
using PhotonBypass.Infra;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Account;

class AccountApplication(
    AccountRepository account_repository,
    PermenantUserRepository permenant_user_repository,
    HistoryRepository history_repository) : IAccountApplication
{
    public async Task<ApiResult<UserModel>> GetUser(string username)
    {
        var user = await account_repository.GetAccount(username) ??
            throw new UserException("کاربر پیدا نشد!");

        var target_area = (await account_repository.GetTargetArea(user.Id))
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
        var user = await account_repository.GetAccount(target) ??
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

    public async Task<ApiResult> EditUser(string target, EditUserContext model)
    {
        var accountLoadingTask = account_repository.GetAccount(target);
        var userLoadingTask = permenant_user_repository.GetUser(target);

        var account = await accountLoadingTask ?? throw new UserException("کاربر پیدا نشد!");
        SetAccount(account, model);

        var user = await userLoadingTask ?? throw new UserException("کاربر پیدا نشد!");
        SetPermanentUser(user, model);

        var savingAccountTask = account_repository.Save(account);
        var savingUserTask = permenant_user_repository.Save(user);

        await savingAccountTask;
        await savingUserTask;

        return ApiResult.Success("ذخیره شد.");
    }

    private static void SetAccount(AccountEntity account, EditUserContext model)
    {
        account.Name = model.Firstname;
        account.Surname = model.Lastname;

        if (account.Email != model.Email)
            account.EmailValid = false;

        if (account.Mobile != model.Mobile)
            account.MobileValid = false;

        account.Email = model.Email;
        account.Mobile = model.Mobile;
    }

    private static void SetPermanentUser(PermenantUserEntity user, EditUserContext model)
    {
        user.Name = model.Firstname;
        user.Surname = model.Lastname;
        user.Email = model.Email;
        user.Phone = model.Mobile;
    }

    public async Task<ApiResult> ChangePassword(string target, string token, string password)
    {
        token = HashHandler.HashPassword(token);
        password = HashHandler.HashPassword(password);

        var account = await account_repository.GetAccount(target) ??
            throw new UserException("کاربر پیدا نشد!");

        if (account.Password != token)
        {
            throw new UserException("کلمه عبور فعلی اشتباه است!");
        }

        account.Password = password;

        await account_repository.Save(account);

        return ApiResult.Success("کلمه عبور تغییر کرد.");
    }

    public async Task<ApiResult<IList<HistoryModel>>> GetHistory(string target, DateTime? from, DateTime? to)
    {
        var records = await history_repository.GetHistory(target, from, to);

        var result = records.Select(history => new HistoryModel
        {
            Color = history.Color,
            Description = history.Description,
            EventTime = history.EventTime,
            EventTimeTitle = history.EventTime.ToPersianString(),
            Id = history.Id,
            Target = target,
            Title = history.Title,
            Unit = history.Unit,
            Value = history.Value,
        });

        return ApiResult<IList<HistoryModel>>.Success(result.ToList());
    }
}
