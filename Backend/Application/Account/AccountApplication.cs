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
    Lazy<IWalletRepository> wallet_repo,
    Lazy<IJobContext> job_context)
    : IAccountApplication
{
    private IAccountRepository AccountRepo { get; } = account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IWalletRepository> WalletRepo { get; } = wallet_repo;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<AccountEntity> GetActiveUser(string username)
    {
        var account = (await AccountRepo.GetAccount(username)) ??
                      throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{username}");

        if (!account.IsActive)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        return account;
    }

    public async Task<ApiResult<UserModel>> GetUser(string username)
    {
        var account = await GetActiveUser(username);

        var target_area = (await AccountRepo.GetActiveTargetArea(account.Id))
            .Select(entity => new TargetModel
            {
                Username = entity.Username,
                Fullname = entity.Fullname,
                Email = entity.Email,
            })
            .ToDictionary(k => k.Username);

        target_area.Add(account.Username, new TargetModel
        {
                Username = account.Username,
                Fullname = account.Fullname,
                Email = account.Email,
        });

        var balance = await WalletRepo.Value.GetBalance(account.Id);

        return ApiResult<UserModel>.Success(new UserModel
        {
            Username = account.Username,
            Balance = balance,
            Email = account.Email,
            Fullname = account.Fullname,
            Picture = account.Picture,
            TargetArea = target_area,
        });
    }

    public async Task<ApiResult<FullUserModel>> GetFullInfo(string target)
    {
        var account = await GetActiveUser(target);

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
        var account = await GetActiveUser(target);

        account.SetFromModel(model);

        await AccountRepo.Save(account);

        return ApiResult.Success("ذخیره شد.");
    }

    public async Task<ApiResult> ChangePassword(string target, string token, string password)
    {
        token = HashHandler.HashPassword(token);
        password = HashHandler.HashPassword(password);

        var account = await AccountRepo.GetAccount(target);

        return await ChangePassword(account, token, password);
    }

    public async Task<ApiResult> ChangePassword(AccountEntity? account, string token, string password)
    {
        if (account is not { IsActive: true } || account.Password != token)
        {
            if (account != null)
            {
                if (account.IsActive)
                {
                    _ = HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
                    {
                        Target = account.Id,
                        Category = EventCategory.Security,
                        Type = EventType.Critical,
                        Title = "امنیت",
                        Description = "تلاش غیرمجاز برای تغییر کلمه عبور!",
                    });
                }

                Log.Warning("[user: {0}] Invalid password (change-pass) for {1}, active={2}",
                    account.Username, account.Username, account.IsActive);
            }

            throw new UserException("کلمه عبور فعلی اشتباه است!");
        }

        account.Password = password;

        await AccountRepo.Save(account);

        _ = HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.Id,
            Category = EventCategory.Security,
            Type = EventType.Success,
            Title = "امنیت",
            Description = "تغییر کلمه عبور اکانت.",
        });

        return ApiResult.Success("کلمه عبور تغییر کرد.");
    }

    public async Task<ApiResult<IList<HistoryModel>>> GetHistory(string target, DateTime? from, DateTime? to)
    {
        var records = await HistoryRepo.Value.GetHistory(target, from, to);
        var issuer_ids = records.Select(h => h.Issuer ?? 0).Where(id => id > 0);
        var issuers = await AccountRepo.GetUsernamesByAccountId(issuer_ids);

        var result = records.Select(history => new HistoryModel
        {
            Category = history.Category,
            Type = history.Type,
            Description = history.Description,
            EventTime = history.Created,
            EventTimeTitle = history.Created.ToPersianString(),
            Id = history.Id,
            Issuer = history.Issuer.HasValue && issuers.TryGetValue(history.Issuer.Value, out var issuer)
                ? issuer
                : null,
            Target = target,
            Title = history.Title,
            Value = history.Value,
            Price = history.Price,
        });

        return ApiResult<IList<HistoryModel>>.Success([.. result]);
    }
}