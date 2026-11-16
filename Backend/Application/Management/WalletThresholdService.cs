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
    Lazy<IPanelUserRepository> panel_user_repo, Lazy<IVpnAccountRepository> vpn_account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IJobContext> job_context) : IWalletThresholdService
{
    private const string DeactivatedValue = "deactivated";
    private const string ActivatedValue = "activated";

    private IOptions<ManagementOptions> Options { get; } = options;
    private IWalletRepository WalletRepo { get; } = wallet_repo;
    private Lazy<IPanelUserRepository> PanelUserRepo { get; } = panel_user_repo;
    private Lazy<IVpnAccountRepository> VpnAccountRepo { get; } = vpn_account_repo;
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

    private async Task Apply(List<int> user_ids, bool deactivate)
    {
        // docs/22 D8: the wallet belongs to the panel user, so the rule sweeps
        // every Active VPN account of the user (docs/20 D13 semantics per
        // account); the state history is one row per user per transition —
        // GetLastByTitle groups by Target, so per-account rows would oscillate
        // for a multi-account user
        var accounts = await VpnAccountRepo.Value.GetVisibleByUsers(user_ids);

        foreach (var user_accounts in accounts
                     .Where(account => account.Status == VpnAccountStatus.Active)
                     .GroupBy(account => account.UserId))
        {
            var usernames = user_accounts.Select(account => account.Username).ToList();

            if (deactivate)
            {
                Log.Warning("[wallet-threshold] Deactivating radius users: (user-id:{0}, targets:{1})",
                    user_accounts.Key, string.Join(",", usernames));

                await AccountRadiusSrv.Value.DeactivateUsers(usernames);
            }
            else
            {
                Log.Information("[wallet-threshold] Activating radius users: (user-id:{0}, targets:{1})",
                    user_accounts.Key, string.Join(",", usernames));

                await AccountRadiusSrv.Value.ActivateUsers(usernames);
            }

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = user_accounts.Key,
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
