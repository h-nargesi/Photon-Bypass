using FluentAssertions;
using Moq;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockXuiPanel;
using PhotonBypass.Xui.Application;

namespace PhotonBypass.Test.Facts.Xui;

public class XuiTrafficSyncTest : UnitLevelServiceInitializer
{
    private static readonly ServerEntity XuiServer = new()
    {
        Id = 50,
        IsActive = true,
        RealmId = 1,
        Name = "XuiServer",
        IpAddress = "10.0.0.50",
        DomainName = "xui.photon-bypass.com",
        Features = ServerFeature.Xui | ServerFeature.V2ray,
    };

    private static void UseXuiServer(IServiceScope scope)
    {
        scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>()
            .Setup(repository => repository.GetAllActiveRadius())
            .ReturnsAsync(new List<ServerEntity> { XuiServer });
    }

    [Fact]
    public async Task SuccessfulPoll_WritesRowsAndRenewalColumnsTogether()
    {
        using var scope = App.Services.CreateScope();

        UseXuiServer(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        var traffic_moq = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();

        api_moq.AddClient("User3", "uuid-1", 400, 600);

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.LastSeenCounter = 500;
        renewal.LastPollTime = DateTime.Now.AddMinutes(-5);
        renewal.TrafficUsed = 500;
        renewal.LastActiveTime = null;

        var before_rows = traffic_moq.Data.Count;

        await scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>().Run();

        var stored = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        stored.LastSeenCounter.Should().Be(1000);
        stored.TrafficUsed.Should().Be(1000);
        stored.LastPollTime.Should().NotBeNull();
        stored.LastActiveTime.Should().NotBeNull();

        traffic_moq.Data.Count.Should().Be(before_rows + 1);

        var row = traffic_moq.Data.Last();
        row.AccountId.Should().Be(3);
        row.NasId.Should().Be(XuiServer.Id);
        row.SessionId.Should().Be(XuiTrafficRow.SessionId(3, 1, DateTime.Now));
        row.TotalData.Should().Be(500);
    }

    [Fact]
    public async Task TrafficWriteFailure_RollsBackRowsAndRenewalColumns()
    {
        using var scope = App.Services.CreateScope();

        UseXuiServer(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        var traffic_moq = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();

        api_moq.AddClient("User3", "uuid-1", 400, 600);

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.LastSeenCounter = 500;
        renewal.LastPollTime = DateTime.Now.AddMinutes(-5);
        renewal.TrafficUsed = 500;
        renewal.LastActiveTime = null;

        var before_rows = traffic_moq.Data.Count;

        traffic_moq.OnBachSave += _ => throw new Exception("traffic write failed");

        await scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>().Run();

        traffic_moq.Data.Count.Should().Be(before_rows);

        var stored = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        stored.LastSeenCounter.Should().Be(500);
        stored.TrafficUsed.Should().Be(500);
        stored.LastPollTime.Should().Be(renewal.LastPollTime);
        stored.LastActiveTime.Should().BeNull();
    }

    [Fact]
    public async Task RenewalWriteFailure_RollsBackTrafficRows()
    {
        using var scope = App.Services.CreateScope();

        UseXuiServer(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        var traffic_moq = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();

        api_moq.AddClient("User3", "uuid-1", 400, 600);

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.LastSeenCounter = 500;
        renewal.LastPollTime = DateTime.Now.AddMinutes(-5);
        renewal.TrafficUsed = 500;

        var before_rows = traffic_moq.Data.Count;

        renewal_moq.Setup(repository => repository.Save(It.IsAny<IEnumerable<RenewalEntity>>()))
            .ThrowsAsync(new Exception("renewal write failed"));

        await scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>().Run();

        traffic_moq.Data.Count.Should().Be(before_rows);

        var stored = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        stored.TrafficUsed.Should().Be(500);
        stored.LastSeenCounter.Should().Be(500);
    }

    [Fact]
    public async Task UnknownClient_IsSkippedAsReconciliationSignal()
    {
        using var scope = App.Services.CreateScope();

        UseXuiServer(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        var traffic_moq = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();

        api_moq.AddClient("GhostClient", "uuid-x", 100, 100);

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.LastSeenCounter = 500;
        renewal.LastPollTime = DateTime.Now.AddMinutes(-5);
        renewal.TrafficUsed = 500;

        var before_rows = traffic_moq.Data.Count;

        var action = () => scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>().Run();

        await action.Should().NotThrowAsync();

        traffic_moq.Data.Count.Should().Be(before_rows);
        renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1).TrafficUsed.Should().Be(500);
    }

    [Fact]
    public async Task RealmLockNotAcquired_SkipsTheServer()
    {
        using var scope = App.Services.CreateScope();

        UseXuiServer(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        var realm_moq = scope.ServiceProvider.GetRequiredService<RealmRepositoryMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        var traffic_moq = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();

        api_moq.AddClient("User3", "uuid-1", 400, 600);

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.LastSeenCounter = 500;
        renewal.LastPollTime = DateTime.Now.AddMinutes(-5);
        renewal.TrafficUsed = 500;

        var before_rows = traffic_moq.Data.Count;

        realm_moq.Setup(repository => repository.TryLockTrafficSync(It.IsAny<IEnumerable<int>>(), It.IsAny<int>()))
            .ReturnsAsync([]);

        await scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>().Run();

        traffic_moq.Data.Count.Should().Be(before_rows);
        renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1).TrafficUsed.Should().Be(500);
    }
}
