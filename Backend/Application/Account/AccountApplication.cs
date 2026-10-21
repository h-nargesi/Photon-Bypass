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
    Lazy<ITelegramLinkRepository> telegram_link_repo,
    Lazy<IJobContext> job_context)
    : IAccountApplication
{
    /// <summary>Lifetime of a Telegram link code (D2: one-time, short-lived).</summary>
    private static readonly TimeSpan TelegramLinkLifetime = TimeSpan.FromMinutes(15);

    private IAccountRepository AccountRepo { get; } = account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IWalletRepository> WalletRepo { get; } = wallet_repo;
    private Lazy<ITelegramLinkRepository> TelegramLinkRepo { get; } = telegram_link_repo;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<AccountEntity> GetActiveUser(string username)
    {
        var account = (await AccountRepo.GetAccount(username)) ??
                      throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{username}");

        if (account.Status == AccountStatus.Deactivated)
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
            Role = account.Role,
            AllowSelfService = account.UserType.HasFlag(UserTypes.AllowSelfService),
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
        var account = await AccountRepo.GetAccount(target);

        return await ChangePassword(account, token, password);
    }

    public async Task<ApiResult> ChangePassword(AccountEntity? account, string token, string password)
    {
        if (account is not { Status: not AccountStatus.Deactivated } || !PasswordHasher.Verify(token, account.Password))
        {
            if (account != null)
            {
                if (account.Status != AccountStatus.Deactivated)
                {
                    await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
                    {
                        Target = account.Id,
                        Category = EventCategory.Security,
                        Type = EventType.Critical,
                        Title = "امنیت",
                        Description = "تلاش غیرمجاز برای تغییر کلمه عبور!",
                    });
                }

                Log.Warning("[user: {0}] Invalid password (change-pass) for {1}, status={2}",
                    account.Username, account.Username, account.Status);
            }

            throw new UserException("کلمه عبور فعلی اشتباه است!");
        }

        return await SetPassword(account, password);
    }

    public async Task<ApiResult> ChangePassword(AccountEntity? account, string password)
    {
        if (account is not { Status: not AccountStatus.Deactivated })
        {
            throw new UserException("کاربر یافت نشد یا غیرفعال است!",
                $"Cannot change password, account is null or inactive.");
        }

        return await SetPassword(account, password);
    }

    private async Task<ApiResult> SetPassword(AccountEntity account, string password)
    {
        // every password-setting path funnels through here (change-pass with
        // the current token, reset-pass codes, admin/manager set-password)
        AccountBusiness.ValidatePassword(password);

        account.Password = PasswordHasher.Hash(password);

        await AccountRepo.Save(account);

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
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

    public async Task<ApiResult<TelegramLinkModel>> CreateTelegramLink(string target)
    {
        var account = await GetActiveUser(target);

        if (account.TelegramChatId != null)
        {
            var connected = ApiResult<TelegramLinkModel>.Success(new TelegramLinkModel { Connected = true });

            connected.Message = "این حساب قبلاً به تلگرام متصل است.";

            return connected;
        }

        // retire the pending codes so only the newest deep link can bind
        await TelegramLinkRepo.Value.ExpireActiveCodes(account.Id);

        var code = HashHandler.GenerateUrlSafeCode();

        await TelegramLinkRepo.Value.AddCode(new TelegramLinkEntity
        {
            AccountId = account.Id,
            Code = code,
            ExpireDate = DateTime.Now + TelegramLinkLifetime,
        });

        Log.Information("[user: {0}] Telegram link code issued: account-id={1}",
            JobContext.Value.Username, account.Id);

        return ApiResult<TelegramLinkModel>.Success(new TelegramLinkModel { Connected = false, Code = code });
    }

    public async Task<ApiResult<TelegramLinkModel>> GetTelegramLinkStatus(string target)
    {
        var account = await GetActiveUser(target);

        return ApiResult<TelegramLinkModel>.Success(new TelegramLinkModel
        {
            Connected = account.TelegramChatId != null,
        });
    }

    public async Task<ApiResult> DisconnectTelegram(string target)
    {
        var account = await GetActiveUser(target);

        if (account.TelegramChatId != null)
        {
            await AccountRepo.SetTelegramChat(account.Id, null);

            Log.Information("[user: {0}] Telegram chat detached: account-id={1}",
                JobContext.Value.Username, account.Id);
        }

        return ApiResult.Success("اتصال تلگرام قطع شد.");
    }
}