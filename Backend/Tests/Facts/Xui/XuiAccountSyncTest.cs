using FluentAssertions;
using Moq;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockXuiPanel;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

public class XuiAccountSyncTest : UnitLevelServiceInitializer
{
    private static readonly ServerEntity Server = new()
    {
        Id = 50,
        IsActive = true,
        RealmId = 1,
        Name = "XuiServer",
        IpAddress = "10.0.0.50",
        DomainName = "xui.photon-bypass.com",
        Features = ServerFeature.Xui | ServerFeature.V2ray,
    };

    [Fact]
    public async Task SyncUserAndActive_NewClient_AddsResetsAndPushesRenewalLimits()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        var account = new AccountEntity
        {
            Id = 3,
            Username = "User3",
            Password = "x",
            VpnPassword = "4c1f3b2a-1111-2222-3333-444455556666",
        };

        var renewal = new RenewalEntity
        {
            Id = 11,
            AccountId = 3,
            SimultaneousUser = 2,
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 30,
            LastSeenCounter = 5000,
            LastPollTime = DateTime.Now.AddHours(-1),
            TrafficUsed = 5000,
        };

        api_moq.AddClient("User3", "old-uuid", 400, 600);

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui);

        await service.SyncUserAndActive(Server, account, renewal);

        api_moq.Calls.Should().Equal("update:User3", "reset:User3");

        var pushed = api_moq.Clients.Single();
        pushed.Email.Should().Be("User3");
        pushed.Uuid.Should().Be(account.VpnPassword);
        pushed.Enable.Should().BeTrue();
        pushed.TotalTrafficBytes.Should().Be(renewal.TrafficLimit);
        pushed.LimitIp.Should().Be(2);
        pushed.ExpiryTime.Should().BeGreaterThan(0);

        renewal.LastSeenCounter.Should().BeNull();
        renewal.LastPollTime.Should().BeNull();

        renewal_moq.Verify(repository => repository.Save(
                It.Is<RenewalEntity>(saved => saved.LastSeenCounter == null && saved.LastPollTime == null)),
            Times.Once);
    }

    [Fact]
    public async Task SyncUserAndActive_UnknownClient_AddsInsteadOfUpdating()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();

        var account = new AccountEntity
        {
            Id = 3,
            Username = "User3",
            Password = "x",
            VpnPassword = null!,
        };

        var renewal = new RenewalEntity
        {
            Id = 11,
            AccountId = 3,
            SimultaneousUser = 1,
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = null,
        };

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui);

        await service.SyncUserAndActive(Server, account, renewal);

        api_moq.Calls.Should().Equal("add:User3", "reset:User3");

        var added = api_moq.Clients.Single();
        added.Uuid.Should().NotBeNullOrEmpty();
        added.ExpiryTime.Should().Be(0, "a traffic-only renewal must encode 'never' as 0");
        added.TotalTrafficBytes.Should().Be(renewal.TrafficLimit);
    }

    [Fact]
    public async Task RemoveUsers_RemovesOnlyManagedClients()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User3", "uuid-1", 0, 0);
        api_moq.AddClient("unknown.client", "uuid-2", 0, 0);

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui);

        await service.RemoveUsers(Server, ["User3", "unknown.client"]);

        api_moq.Calls.Should().Equal("remove:User3");
        api_moq.Clients.Single().Email.Should().Be("unknown.client");
    }

    [Fact]
    public async Task DeactivateUserExcept_DisablesEnabledClientsOutsideTheValidSet()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User3", "uuid-1", 0, 0);
        api_moq.AddClient("User4", "uuid-2", 0, 0);
        api_moq.Clients[1].Enable = false;

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui);

        await service.DeactivateUserExcept(Server, ["User4"]);

        api_moq.Calls.Should().BeEquivalentTo(["update:User3"]);
        api_moq.Clients[0].Enable.Should().BeFalse();
        api_moq.Clients[1].Enable.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeVpnPassword_RotatesUuidAsPatchOnlyUpdate()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        api_moq.AddClient("User3", "old-live-uuid", 400, 600);

        var seeded = api_moq.Clients.Single();
        seeded.TotalTrafficBytes = 26_843_545_600L;
        seeded.ExpiryTime = 1_760_000_000_000L;
        seeded.LimitIp = 3;
        seeded.Enable = true;

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui);

        var result = await service.ChangeVpnPassword(Server, "User3", "4c1f3b2a-1111-2222-3333-444455556666");

        // the rotation returns the applied credential (docs/16 D4 contract)
        result.Should().Be("4c1f3b2a-1111-2222-3333-444455556666");

        // patch-only: a single update keyed by the OLD live uuid, no traffic
        // reset and no renewal-anchor rewrite (unlike SyncUserAndActive)
        api_moq.Calls.Should().Equal("update:User3");
        api_moq.Verify(api => api.UpdateClient(It.IsAny<ServerEntity>(), "old-live-uuid",
            It.Is<XuiClientModel>(client => client.Uuid == "4c1f3b2a-1111-2222-3333-444455556666")), Times.Once);

        var rotated = api_moq.Clients.Single();
        rotated.Uuid.Should().Be("4c1f3b2a-1111-2222-3333-444455556666");
        rotated.TotalTrafficBytes.Should().Be(26_843_545_600L);
        rotated.ExpiryTime.Should().Be(1_760_000_000_000L);
        rotated.LimitIp.Should().Be(3);
        rotated.Enable.Should().BeTrue();

        api_moq.Counters["User3"].Should().Be((400, 600));

        renewal_moq.Verify(repository => repository.Save(It.IsAny<RenewalEntity>()), Times.Never);
    }

    [Fact]
    public async Task ChangeVpnPassword_DisabledClient_StaysDisabledAfterRotation()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();

        api_moq.AddClient("User3", "old-live-uuid", 0, 0);
        api_moq.Clients.Single().Enable = false;

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui);

        await service.ChangeVpnPassword(Server, "User3", "new-uuid");

        // patch-only contract: rotation swaps the UUID and never flips enable —
        // re-activation is exclusively the sync/monitoring path's business
        api_moq.Clients.Single().Enable.Should().BeFalse();
        api_moq.Calls.Should().Equal("update:User3");
    }

    [Fact]
    public async Task ChangeVpnPassword_MissingClient_FailsLoudWithUserException()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("SomeoneElse", "uuid-1", 0, 0);

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.Xui);

        var action = () => service.ChangeVpnPassword(Server, "User3", "new-uuid");

        (await action.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("شناسه‌ی اتصال شما روی سرور یافت نشد");
        api_moq.Calls.Should().BeEmpty();
    }
}
