using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Test.Mock.MockXuiPanel;
using PhotonBypass.Xui.WebService;
using tik4net;

namespace PhotonBypass.Test.Facts.Radius;

public class AccountRadiusTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task SyncUserAndActive_CreatesLimitationProfileAndAssignmentMatchingRenewal()
    {
        using var scope = App.Services.CreateScope();
        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();
        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();

        var account = new VpnAccountEntity
        {
            Id = 3,
            RealmId = 3,
            Username = "User3",
            VpnPassword = "VpnPassword",
        };

        var renewal = new RenewalEntity
        {
            VpnAccountId = 3,
            SimultaneousUser = 1,
            TimeLimitInDays = 120,
            TrafficLimit = 25L * 1024 * 1024 * 1024,
        };

        var executed = new ConcurrentQueue<(string Command, List<ITikCommandParameter> Parameters)>();
        tik4_net_moq.OnExecute += (command_text, parameters) =>
            executed.Enqueue((command_text, parameters));

        await account_radius.SyncUserAndActive(account, renewal);

        var commands = executed.Select(entry => entry.Command).ToArray();

        // the dispatcher provisions only the servers of the account's realm
        // (realm 3, Docs/22 D5); the empty limitation/profile seed then forces
        // creation before the user is bound
        commands.Should().Equal(
        [
            "/user-manager/limitation/add",
            "/user-manager/profile/add",
            "/user-manager/profile-limitation/add",
            "/user-manager/user-profile/add",
        ]);

        var limitation = executed.ElementAt(0);
        limitation.Parameters.Should().Contain(p => p.Name == "name" && p.Value == "limit-traffic-25G");
        limitation.Parameters.Should().Contain(p => p.Name == "transfer-limit" &&
                                                    p.Value == (25L * 1024 * 1024 * 1024).ToString());

        var profile = executed.ElementAt(1);
        profile.Parameters.Should().Contain(p => p.Name == "name" && p.Value == "profile-25G-120d");
        profile.Parameters.Should().Contain(p => p.Name == "validity" && p.Value == "120d 00:00:00");
        profile.Parameters.Should().Contain(p => p.Name == "name-for-users" && p.Value == "25 Gigabytes 120 Days");

        var assignment = executed.ElementAt(2);
        assignment.Parameters.Should().Contain(p => p.Name == "profile" && p.Value == "profile-25G-120d");
        assignment.Parameters.Should().Contain(p => p.Name == "limitation" && p.Value == "limit-traffic-25G");

        var user_profile = executed.ElementAt(3);
        user_profile.Parameters.Should().Contain(p => p.Name == "user" && p.Value == "User3");
        user_profile.Parameters.Should().Contain(p => p.Name == "profile" && p.Value == "profile-25G-120d");
    }

    [Fact]
    public async Task SyncUserAndActive_WithDifferentSimultaneousUserCount_UpdatesTheUser()
    {
        using var scope = App.Services.CreateScope();
        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();
        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();

        var account = new VpnAccountEntity
        {
            Id = 3,
            RealmId = 3,
            Username = "User3",
            VpnPassword = "VpnPassword",
        };

        var renewal = new RenewalEntity
        {
            VpnAccountId = 3,
            SimultaneousUser = 2,
            TimeLimitInDays = 120,
            TrafficLimit = 25L * 1024 * 1024 * 1024,
        };

        var user_sets = new ConcurrentQueue<List<ITikCommandParameter>>();
        tik4_net_moq.OnExecute += (command_text, parameters) =>
        {
            if (command_text == "/user-manager/user/set")
            {
                user_sets.Enqueue(parameters);
            }
        };

        await account_radius.SyncUserAndActive(account, renewal);

        // realm 3 has a single radius server: only the shared-users update runs
        user_sets.Should().HaveCount(1);
        var shared_update = user_sets.Single(parameters =>
            parameters.Any(p => p.Name == "shared-users"));
        shared_update.Should().Contain(p => p.Name == ".id" && p.Value == "3");
        shared_update.Should().Contain(p => p.Name == "shared-users" && p.Value == "2");
    }

    [Fact]
    public async Task DeactivateInvalidRadiusUsers_UnknownRealm_ShouldNotThrow()
    {
        using var scope = App.Services.CreateScope();

        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();

        var plan_state_list = await plan_state_repo.GetAll();

        // realm 4 hosts an active radius server but has no plan-state rows
        // (every account pins a realm now — Docs/22 §2 — so realm 4 has no
        // valid set at all and its sweep disables every user there)
        Assert.DoesNotContain(plan_state_list, plan => plan.RealmId is 4);

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

        // realm 4 (no plan-state rows) deactivates every user on its radius
        // server; realm 1 keeps only User99 and realm 3 only User3 — an
        // account is valid exclusively on the realm it is pinned to
        // (the bucket-0 fan-out is gone, Docs/22 §2)
        Assert.Equal(2, disabled_counter.GetValueOrDefault("User99"));
        Assert.Equal(2, disabled_counter.GetValueOrDefault("User2"));
        Assert.Equal(2, disabled_counter.GetValueOrDefault("User3"));
        Assert.Equal(2, disabled_counter.GetValueOrDefault("User4"));
    }

    [Fact]
    public async Task DeactivateInvalidRadiusUsers_NonActiveStatus_DropsFromValidSet()
    {
        using var scope = App.Services.CreateScope();

        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();

        var plan_state_list = await plan_state_repo.GetAll();

        // a management-suspended or deactivated account leaves the valid set
        // of its own realm too (Docs/20 D13): the hourly sweep is the safety
        // net that disables it everywhere even when the immediate sync failed
        plan_state_list.Single(plan => plan.Username == "User3").Status = VpnAccountStatus.Suspended;
        plan_state_list.Single(plan => plan.Username == "User99").Status = VpnAccountStatus.Deactivated;

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

        // baseline with every status Active (see UnknownRealm test above):
        // User99 and User3 are disabled only on realms 3/4 and 1/4 (count 2);
        // non-Active they now drop out of their own realm's valid set as well (count 3)
        Assert.Equal(3, disabled_counter.GetValueOrDefault("User99"));
        Assert.Equal(3, disabled_counter.GetValueOrDefault("User3"));
    }

    [Fact]
    public async Task ChangeVpnPassword_RadiusRealm_AppliesAndReturnsThePassword()
    {
        using var scope = App.Services.CreateScope();

        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();
        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();

        var applied = new ConcurrentQueue<(string Command, List<ITikCommandParameter> Parameters)>();
        tik4_net_moq.OnExecute += (command_text, parameters) =>
        {
            if (command_text == "/user-manager/user/set")
            {
                applied.Enqueue((command_text, parameters));
            }
        };

        var result = await account_radius.ChangeVpnPassword(3, "User3", "new-radius-password");

        result.Should().Be("new-radius-password");
        applied.Should().ContainSingle();
        applied.Single().Parameters.Should().Contain(p => p.Name == "password" && p.Value == "new-radius-password");
    }

    [Fact]
    public async Task ChangeVpnPassword_XuiRealm_GeneratesOneUuidForAllServers()
    {
        using var scope = App.Services.CreateScope();

        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(2))
            .ReturnsAsync([NewXuiServer(51), NewXuiServer(52)]);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User3", "old-uuid", 10, 20);

        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();

        var result = await account_radius.ChangeVpnPassword(2, "User3", "caller-supplied-password");

        Guid.TryParse(result, out _).Should().BeTrue("the dispatcher must generate the UUID, not the caller input");
        result.Should().NotBe("caller-supplied-password");

        api_moq.Verify(api => api.UpdateClient(It.IsAny<ServerEntity>(), It.IsAny<string>(),
            It.Is<XuiClientModel>(client => client.Uuid == result)), Times.Exactly(2));
    }

    [Fact]
    public async Task ChangeVpnPassword_MixedRealm_FailsLoudBeforeAnyServerIsTouched()
    {
        using var scope = App.Services.CreateScope();

        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(2))
            .ReturnsAsync([
                NewXuiServer(51),
                new ServerEntity
                {
                    Id = 52,
                    IsActive = true,
                    RealmId = 2,
                    Name = "UserManagerServer",
                    IpAddress = "10.0.0.52",
                    DomainName = "um.photon-bypass.com",
                    Features = ServerFeature.UserManager,
                },
            ]);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User3", "old-uuid", 0, 0);

        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();
        var radius_touched = false;
        tik4_net_moq.OnExecute += (_, _) => radius_touched = true;

        var account_radius = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();

        var action = () => account_radius.ChangeVpnPassword(2, "User3", "new-password");

        (await action.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("تغییر شناسه‌ی اتصال برای این سرور پشتیبانی نمی‌شود");
        api_moq.Calls.Should().BeEmpty("the mixed-realm gate must fire before the fan-out");
        radius_touched.Should().BeFalse();
    }

    private static ServerEntity NewXuiServer(int id)
    {
        return new ServerEntity
        {
            Id = id,
            IsActive = true,
            RealmId = 2,
            Name = $"XuiServer{id}",
            IpAddress = $"10.0.0.{id}",
            DomainName = $"xui{id}.photon-bypass.com",
            Features = ServerFeature.Xui | ServerFeature.V2ray,
        };
    }
}
