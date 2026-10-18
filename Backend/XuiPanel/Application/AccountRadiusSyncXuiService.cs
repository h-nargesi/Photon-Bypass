using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Xui.Application;

public class AccountRadiusSyncXuiService(
    Lazy<IXuiApi> api,
    Lazy<IRenewalRepository> renewal_repo) : IInfraAccountRadiusSyncService
{
    private Lazy<IXuiApi> Api { get; } = api;
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;

    public async Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames)
    {
        var target_set = usernames.Where(IsManagedClient).ToHashSet();

        if (target_set.Count == 0) return;

        var clients = await Api.Value.ListClients(radius);

        foreach (var client in clients.Where(client => target_set.Contains(client.Email)))
        {
            await Api.Value.RemoveClient(radius, client.Uuid);
        }
    }

    public async Task DeactivateUserExcept(ServerEntity radius, HashSet<string> usernames)
    {
        var clients = await Api.Value.ListClients(radius);

        foreach (var client in clients.Where(client => IsManagedClient(client.Email) &&
                                                       !usernames.Contains(client.Email) &&
                                                       client.Enable))
        {
            client.Enable = false;

            await Api.Value.UpdateClient(radius, client.Uuid, client);
        }
    }

    public async Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        // one full listing for the whole batch: a per-username GetClient costs
        // a complete inbound fetch each time, and the hourly sweeps call this
        // with many users at once
        var target_set = usernames.Where(IsManagedClient).ToHashSet();

        if (target_set.Count == 0)
        {
            return;
        }

        foreach (var client in (await Api.Value.ListClients(radius))
                     .Where(client => target_set.Contains(client.Email) && client.Enable))
        {
            client.Enable = false;

            await Api.Value.UpdateClient(radius, client.Uuid, client);
        }
    }

    public async Task ActivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        var target_set = usernames.Where(IsManagedClient).ToHashSet();

        if (target_set.Count == 0)
        {
            return;
        }

        foreach (var client in (await Api.Value.ListClients(radius))
                     .Where(client => target_set.Contains(client.Email) && !client.Enable))
        {
            client.Enable = true;

            await Api.Value.UpdateClient(radius, client.Uuid, client);
        }
    }

    public async Task SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal)
    {
        var uuid = string.IsNullOrEmpty(account.VpnPassword)
            ? Guid.NewGuid().ToString()
            : account.VpnPassword;

        var client = await Api.Value.GetClient(radius, account.Username);

        if (client == null)
        {
            await Api.Value.AddClient(radius, XuiEncoding.BuildClient(account.Username, uuid, renewal));
        }
        else
        {
            // Key by the UUID currently live in the panel: statistics are keyed
            // by email, so the update preserves up/down counters even when the
            // UUID drifts from the database (docs/17 D13 UUID-rotation branch).
            await Api.Value.UpdateClient(radius, client.Uuid, XuiEncoding.BuildClient(account.Username, uuid, renewal));
        }

        await Api.Value.ResetClientTraffic(radius, account.Username);

        if (renewal.Id > 0)
        {
            renewal.LastSeenCounter = null;
            renewal.LastPollTime = null;

            await RenewalRepo.Value.Save(renewal);
        }
    }

    public async Task<string> ChangeVpnPassword(ServerEntity radius, string username, string uuid)
    {
        var client = await Api.Value.GetClient(radius, username)
                  ?? throw new UserException(
                         "شناسه‌ی اتصال شما روی سرور یافت نشد؛ لطفاً با پشتیبانی تماس بگیرید.",
                         $"The x-ui client is missing for credential rotation: (server={radius.Name}, user={username})");

        // Rotation is a patch-only UUID swap (docs/16 D4): the update is keyed by
        // the UUID currently live in the panel (statistics are email-keyed and
        // survive the change), quota/expiry/enable pass through untouched and no
        // traffic reset or renewal-anchor rewrite may happen here.
        var live_uuid = client.Uuid;

        client.Uuid = uuid;

        await Api.Value.UpdateClient(radius, live_uuid, client);

        return uuid;
    }

    private static bool IsManagedClient(string email)
    {
        return !string.IsNullOrEmpty(email) && AccountBusiness.UsernamePattern().Match(email).Success;
    }
}
