using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Account;

class AccountApplication(
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<IAccountRadiusSyncService> RadiusSrv,
    Lazy<IJobContext> JobContext)
    : IAccountApplication
{
    public async Task<ApiResult<UserModel>> GetUser(string username)
    {
        var user = await AccountRepo.Value.GetAccount(username) ??
            throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{username}");

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
            throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{target}");

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

    public async Task<ApiResult> EditUser(string target, EditUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Mobile))
        {
            throw new UserException("حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        var account = await AccountRepo.Value.GetAccount(target) ??
            throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{target}");
        account.SetFromModel(model);

        Task? radiusSyncTask;
        if (RadiusSrv.IsValueCreated)
        {
            radiusSyncTask = RadiusSrv.Value.SaveUserPersonalInfo(account);
        }
        else radiusSyncTask = null;

        await AccountRepo.Value.Save(account);

        if (radiusSyncTask != null)
        {
            await radiusSyncTask;
        }

        return ApiResult.Success("ذخیره شد.");
    }

    public async Task<ApiResult> ChangePassword(string target, string token, string password)
    {
        token = HashHandler.HashPassword(token);
        password = HashHandler.HashPassword(password);

        var account = await AccountRepo.Value.GetAccount(target);

        if (account == null || account.Password != token)
        {
            if (account != null)
            {
                _ = HistoryRepo.Value.Save(new HistoryEntity
                {
                    Issuer = JobContext.Value.Username,
                    Target = account.Username,
                    EventTime = DateTime.Now,
                    Title = "امنیت",
                    Description = "تلاش غیرمجاز برای تغییر کلمه عبور!",
                });

                Log.Warning("[user: {0}] Invlid password (change-pass) for {1}", account.Username, target);
            }

            throw new UserException("کلمه عبور فعلی اشتباه است!");
        }

        account.Password = password;

        await AccountRepo.Value.Save(account);

        _ = HistoryRepo.Value.Save(new HistoryEntity
        {
            Issuer = JobContext.Value.Username,
            Target = target,
            EventTime = DateTime.Now,
            Title = "امنیت",
            Description = "تغییر کلمه عبور اکانت.",
        });

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
            Issuer = history.Issuer,
            Target = history.Target,
            Title = history.Title,
            Unit = history.Unit,
            Value = history.Value,
        });

        return ApiResult<IList<HistoryModel>>.Success([.. result]);
    }
}
