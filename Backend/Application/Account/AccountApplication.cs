using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Database;
using PhotonBypass.Domain.Local;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra;
using PhotonBypass.Infra.Controller;
using PhotonBypass.OutSource;

namespace PhotonBypass.Application.Account;

class AccountApplication(
    Lazy<AccountRepository> AccountRepo,
    Lazy<PermenantUsersRepository> UserRepo,
    Lazy<HistoryRepository> HistoryRepo,
    Lazy<IRadiusDeskService> RadiusDeskSrv)
    : IAccountApplication
{
    public async Task<ApiResult<UserModel>> GetUser(string username)
    {
        var user = await AccountRepo.Value.GetAccount(username) ??
            throw new UserException("کاربر پیدا نشد!");

        var target_area = (await AccountRepo.Value.GetTargetArea(user.Id))
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
        var user = await AccountRepo.Value.GetAccount(target) ??
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
        var userLoadingTask = UserRepo.Value.GetUser(target);
        var accountLoadingTask = AccountRepo.Value.GetAccount(target);

        var account = await accountLoadingTask ?? throw new UserException("کاربر پیدا نشد!");
        SetAccount(account, model);

        var user = await userLoadingTask ?? throw new UserException("کاربر پیدا نشد!");
        SetPermanentUser(user, model);

        var savingUserTask = RadiusDeskSrv.Value.SavePermenentUser(user);
        var savingAccountTask = AccountRepo.Value.Save(account);

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

        var account = await AccountRepo.Value.GetAccount(target) ??
            throw new UserException("کاربر پیدا نشد!");

        if (account.Password != token)
        {
            throw new UserException("کلمه عبور فعلی اشتباه است!");
        }

        account.Password = password;

        await AccountRepo.Value.Save(account);

        return ApiResult.Success("کلمه عبور تغییر کرد.");
    }

    public async Task<ApiResult<IList<HistoryModel>>> GetHistory(string target, DateTime? from, DateTime? to)
    {
        var records = await HistoryRepo.Value.GetHistory(target, from, to);

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

        return ApiResult<IList<HistoryModel>>.Success([.. result]);
    }
}
