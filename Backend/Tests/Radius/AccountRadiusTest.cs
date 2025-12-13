using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.Radius;

public class AccountRadiusTest : ServiceInitializer
{
    [Fact]
    public async Task SyncUserAndActive()
    {
        using var scope = App.Services.CreateScope();
        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();

        var account = new AccountEntity
        {
            Id = 3,
            Username = "User3",
            VpnPassword = "VpnPassword",
        };

        var renewal = new RenewalEntity
        {
            AccountId = 3,
            RestrictedRealmId = 3,
            SimultaneousUser = 1,
            TimeLimitInDays = 120,
            TrafficLimit = 25L * 1024 * 1024 * 1024,
        };

        await account_radius.SyncUserAndActive(account, renewal);
    }
}
