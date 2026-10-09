using Microsoft.Extensions.Options;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using Serilog;

namespace PhotonBypass.Application.Management;

public interface IWalletThresholdService
{
    public const string HistoryTitle = "آستانه کیف پول";

    Task CheckAndApply();
}

class WalletThresholdService(
    IOptions<ManagementOptions> options,
    IWalletRepository wallet_repo,
    Lazy<IAccountRepository> account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IJobContext> job_context) : IWalletThresholdService
{
    private const string DeactivatedValue = "deactivated";
    private const string ActivatedValue = "activated";

    private IOptions<ManagementOptions> Options { get; } = options;
    private IWalletRepository WalletRepo { get; } = wallet_repo;
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task CheckAndApply()
    {
        var threshold = Options.Value.WalletDeactivationThreshold;

        if (threshold == null)
        {
            return;
        }

        var below = (await WalletRepo.GetAccountIdsBelowThreshold(threshold.Value)).ToHashSet();
        var last_states = await HistoryRepo.Value.GetLastByTitle(IWalletThresholdService.HistoryTitle);

        var to_deactivate = below
            .Where(id => last_states.GetValueOrDefault(id)?.Value != DeactivatedValue)
            .ToList();

        var to_activate = last_states
            .Where(p => p.Value.Value == DeactivatedValue && !below.Contains(p.Key))
            .Select(p => p.Key)
            .ToList();

        if (to_deactivate.Count > 0)
        {
            await Apply(to_deactivate, deactivate: true);
        }

        if (to_activate.Count > 0)
        {
            await Apply(to_activate, deactivate: false);
        }
    }

    private async Task Apply(List<int> account_ids, bool deactivate)
    {
        var accounts = await AccountRepo.Value.GetActiveAccounts(account_ids);

        foreach (var account in accounts.Values)
        {
            if (deactivate)
            {
                Log.Warning("[wallet-threshold] Deactivating radius users: (target:{0}, account-id:{1})",
                    account.Username, account.Id);

                await AccountRadiusSrv.Value.DeactivateUsers([account.Username]);
            }
            else
            {
                Log.Information("[wallet-threshold] Activating radius users: (target:{0}, account-id:{1})",
                    account.Username, account.Id);

                await AccountRadiusSrv.Value.ActivateUsers([account.Username]);
            }

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.Id,
                Category = EventCategory.Transaction,
                Type = EventType.Information,
                Title = IWalletThresholdService.HistoryTitle,
                Value = deactivate ? DeactivatedValue : ActivatedValue,
                Description = deactivate
                    ? "موجودی زیر آستانه؛ کانفیگ‌ها غیرفعال شد."
                    : "موجودی بالای آستانه؛ کانفیگ‌ها فعال شد.",
            });
        }
    }
}
