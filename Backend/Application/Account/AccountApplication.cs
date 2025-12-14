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
    IAccountRepository account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IJobContext> job_context)
    : IAccountApplication
{
    private IAccountRepository AccountRepo { get; } = account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IJobContext> JobContext { get; } = job_context;
    
    public async Task<ApiResult<UserModel>> GetUser(string username)
    {
        var account = (await AccountRepo.GetAccount(username)) ??
                      throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{username}");

        if (!account.Active)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        var target_area = (await AccountRepo.GetTargetArea(account.Id))
            .Select(entity => new TargetModel
            {
                Username = entity.Username,
                Fullname = entity.Fullname,
                Email = entity.Email,
            })
            .ToDictionary(k => k.Username);

        return ApiResult<UserModel>.Success(new UserModel
        {
            Username = account.Username,
            Balance = account.Balance,
            Email = account.Email,
            Fullname = account.Fullname,
            Picture = account.Picture,
            TargetArea = target_area,
        });
    }

    public async Task<ApiResult<FullUserModel>> GetFullInfo(string target)
    {
        var account = (await AccountRepo.GetAccount(target)) ??
                      throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{target}");

        if (!account.Active)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        return ApiResult<FullUserModel>.Success(new FullUserModel
        {
            Username = account.Username,
            Firstname = account.Name,
            Lastname = account.Surname,
            Email = account.Email,
            EmailValid = account.IsEmailValid,
            Mobile = account.Mobile,
            MobileValid = account.IsMobileValid,
        });
    }

    public async Task<ApiResult> EditUser(string target, EditUserModel model)
    {
        var account = (await AccountRepo.GetAccount(target)) ??
                      throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{target}");

        if (!account.Active)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        account.SetFromModel(model);

        await AccountRepo.Save(account);

        return ApiResult.Success("ذخیره شد.");
    }

    public async Task<ApiResult> ChangePassword(string target, string token, string password)
    {
        token = HashHandler.HashPassword(token);
        password = HashHandler.HashPassword(password);

        var account = await AccountRepo.GetAccount(target);

        if (account is not { Active: true } || account.Password != token)
        {
            if (account != null)
            {
                if (account.Active)
                {
                    _ = HistoryRepo.Value.Save(new HistoryEntity
                    {
                        Issuer = JobContext.Value.Username,
                        Target = account.Username,
                        EventTime = DateTime.Now,
                        Title = "امنیت",
                        Description = "تلاش غیرمجاز برای تغییر کلمه عبور!",
                    });
                }

                Log.Warning("[user: {0}] Invalid password (change-pass) for {1}, active={2}", account.Username, target, account.Active);
            }

            throw new UserException("کلمه عبور فعلی اشتباه است!");
        }

        account.Password = password;

        await AccountRepo.Save(account);

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