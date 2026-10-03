using System.Collections.Concurrent;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.Radius;

public class AccountRadiusTest : UnitLevelServiceInitializer
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

    [Fact]
    public async Task DeactivateInvalidRadiusUsers_UnknownRealm_ShouldNotThrow()
    {
        using var scope = App.Services.CreateScope();

        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();

        var plan_state_list = await plan_state_repo.GetAll();

        // realms 1 and 4 host active radius servers but have no plan-state rows
        Assert.DoesNotContain(plan_state_list, plan => plan.RestrictedRealmId is 1 or 4);

        var user_data_dictionary = tik4_net_moq.GetData<UserModel>()
            .ToDictionary(user => user.Id ?? string.Empty, user => user.Name);

        var disabled_counter = new ConcurrentDictionary<string, int>();

        tik4_net_moq.OnExecute += (command_text, parameters) =>
        {
            if (!command_text.EndsWith("/set")) return;

            var id = parameters.Where(parameter => parameter.Name == ".id")
                .Select(parameter => parameter.Value)
                .FirstOrDefault();

            if (id == null || !user_data_dictionary.TryGetValue(id, out var username)) return;

            disabled_counter.AddOrUpdate(username, 1, (_, count) => count + 1);
        };

        await account_radius.DeactivateInvalidRadiusUsers(plan_state_list);

        // unknown realms (1, 4) deactivate every user on their radius server;
        // realm 3 keeps its own user plus the unrestricted (realm-less) users
        Assert.Equal(2, disabled_counter.GetValueOrDefault("User99"));
        Assert.Equal(3, disabled_counter.GetValueOrDefault("User2"));
        Assert.Equal(2, disabled_counter.GetValueOrDefault("User3"));
        Assert.Equal(3, disabled_counter.GetValueOrDefault("User4"));
    }
}
