using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
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
        foreach (var username in usernames.Where(IsManagedClient))
        {
            await Api.Value.RemoveClient(radius, username);
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

            await Api.Value.UpdateClient(radius, client);
        }
    }

    public async Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        foreach (var username in usernames.Where(IsManagedClient))
        {
            var client = await Api.Value.GetClient(radius, username);

            if (client is not { Enable: true })
            {
                continue;
            }

            client.Enable = false;

            await Api.Value.UpdateClient(radius, client);
        }
    }

    public async Task ActivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        foreach (var username in usernames.Where(IsManagedClient))
        {
            var client = await Api.Value.GetClient(radius, username);

            if (client is not { Enable: false })
            {
                continue;
            }

            client.Enable = true;

            await Api.Value.UpdateClient(radius, client);
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
            await Api.Value.UpdateClient(radius, XuiEncoding.BuildClient(account.Username, uuid, renewal));
        }

        await Api.Value.ResetClientTraffic(radius, account.Username);

        if (renewal.Id > 0)
        {
            renewal.LastSeenCounter = null;
            renewal.LastPollTime = null;

            await RenewalRepo.Value.Save(renewal);
        }
    }

    public Task ChangeVpnPassword(ServerEntity radius, string username, string password)
    {
        throw new NotImplementedException(
            "x-ui credential change is an automatic UUID rotation (docs/16 D4); it needs the " +
            "return-effective-credential contract change before it can be enabled.");
    }

    private static bool IsManagedClient(string email)
    {
        return !string.IsNullOrEmpty(email) && AccountBusiness.UsernamePattern().Match(email).Success;
    }
}
