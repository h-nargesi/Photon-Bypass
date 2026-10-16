using PhotonBypass.Application.Account;
using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Billing;

/// <summary>
/// Admin wallet operations (docs/20 phase 3, D5): transparent manual
/// adjustments (append-only ledger row with the acting admin) and the
/// per-account wallet history with server-side paging.
/// </summary>
public interface IWalletApplication
{
    /// <summary>
    /// Records a manual Completed credit/debit on the target's wallet with the
    /// acting admin as <c>ActorId</c>. Negative balances are allowed (D5);
    /// the deactivation threshold rule runs afterwards.
    /// </summary>
    Task<ApiResult> Adjust(BalanceDirection direction, int amount, string? description);

    /// <summary>
    /// Wallet history of the request target: the account owner itself (with
    /// AllowSelfService) or the admin — a manager never sees a subordinate's
    /// wallet (docs/20 §3).
    /// </summary>
    Task<ApiResult<PagedResult<WalletTransactionModel>>> GetHistory(int page);
}

class WalletApplication(
    IWalletRepository wallet_repo,
    Lazy<IAccountRepository> account_repo,
    Lazy<IAccountApplication> account_app,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IWalletThresholdService> wallet_threshold_srv,
    Lazy<ISelfServiceGuard> self_service_guard,
    Lazy<IJobContext> job_context) : IWalletApplication
{
    private const int HistoryPageSize = 20;

    private const int DescriptionMaxLength = 127;

    private const string FinanceHistoryTitle = "مالی";

    private const string AdjustHistoryDescription = "تراکنش دستی کیف پول توسط ادمین ثبت شد.";

    private IWalletRepository WalletRepo { get; } = wallet_repo;

    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;

    private Lazy<IAccountApplication> AccountApp { get; } = account_app;

    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;

    private Lazy<IWalletThresholdService> WalletThresholdSrv { get; } = wallet_threshold_srv;

    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;

    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult> Adjust(BalanceDirection direction, int amount, string? description)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new UserException("نوع تراکنش نامعتبر است!", $"Invalid adjust direction: {direction}");
        }

        if (amount < 1)
        {
            throw new UserException("مبلغ وارد شده معتبر نیست!", $"Invalid adjust amount: {amount}");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new UserException("توضیح تراکنش الزامی است!", "Manual wallet adjustments require a description (docs/20 D5).");
        }

        description = description.Trim();

        if (description.Length > DescriptionMaxLength)
        {
            throw new UserException("توضیح تراکنش بیش از حد طولانی است!",
                $"Adjust description too long: {description.Length} > {DescriptionMaxLength}");
        }

        // ResolvePayingActor keeps the D6 rule symmetric: adjusting one's own
        // wallet is a self financial operation, acting on another account is not.
        var actor = await SelfServiceGuard.Value.ResolvePayingActor();
        var target = await AccountApp.Value.GetActiveUser(JobContext.Value.Target);

        await WalletRepo.Save(new WalletEntity
        {
            AccountId = target.Id,
            Amount = amount,
            Direction = direction,
            Status = BalanceStatus.Completed,
            Description = description,
            ActorId = actor.Id,
        });

        // A debit can push the account below the deactivation threshold (D5:
        // negative balance is allowed — the threshold rule is the antidote).
        await WalletThresholdSrv.Value.CheckAndApply();

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = target.Id,
            Category = EventCategory.Transaction,
            Type = EventType.Information,
            Title = FinanceHistoryTitle,
            Description = AdjustHistoryDescription,
            Price = amount * (int)direction,
        });

        Log.Information("Manual wallet adjustment: actor-id={0}, target-id={1}, direction={2}, amount={3}",
            actor.Id, target.Id, direction, amount);

        return ApiResult.Success("تراکنش کیف پول ثبت شد.");
    }

    public async Task<ApiResult<PagedResult<WalletTransactionModel>>> GetHistory(int page)
    {
        if (page < 1)
        {
            throw new UserException("شماره صفحه نامعتبر است!", $"Invalid wallet history page: {page}");
        }

        await EnsureHistoryAccess();

        var target = await AccountApp.Value.GetActiveUser(JobContext.Value.Target);

        var (rows, total) = await WalletRepo.GetHistory(target.Id, page, HistoryPageSize);

        var actors = await AccountRepo.Value.GetUsernamesByAccountId(
            rows.Where(r => r.ActorId.HasValue).Select(r => r.ActorId!.Value));

        var items = rows.Select(row => new WalletTransactionModel
        {
            Id = row.Id,
            Amount = row.Amount,
            Direction = row.Direction,
            Status = row.Status,
            Description = row.Description,
            InvoiceCode = row.InvoiceCode,
            Actor = row.ActorId.HasValue && actors.TryGetValue(row.ActorId.Value, out var actor) ? actor : null,
            EventTime = row.Created,
            EventTimeTitle = row.Created.ToPersianString(),
        });

        return ApiResult<PagedResult<WalletTransactionModel>>.Success(new PagedResult<WalletTransactionModel>
        {
            Page = page,
            PageSize = HistoryPageSize,
            TotalCount = total,
            Items = [.. items],
        });
    }

    private async Task EnsureHistoryAccess()
    {
        var context = JobContext.Value;

        if (context.Target == context.Username)
        {
            // own wallet: the dependent gate applies (D6)
            await SelfServiceGuard.Value.EnsureSelfService();
            return;
        }

        // On-behalf view: only the admin may read another account's wallet —
        // even a manager is refused a subordinate's wallet (docs/20 §3).
        var actor = await SelfServiceGuard.Value.ResolveActor();

        if (actor.Role != AccountRole.Admin)
        {
            throw new UserException("مشاهده کیف پول کاربر دیگر مجاز نیست!",
                $"Wallet history of another account is admin-only (docs/20 phase 3): actor={actor.Username}, target={context.Target}",
                403);
        }
    }
}
