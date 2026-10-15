using FluentAssertions;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockXuiPanel;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

public class XuiSessionSyncTest : UnitLevelServiceInitializer
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
    public async Task GetActiveConnections_MapsOnlineIpsToConnectionBindings()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();

        api_moq.OnlineClients.Add(new XuiOnlineClientModel
        {
            Email = "User3",
            Ips = new Dictionary<string, long>
            {
                ["1.2.3.4"] = 1_760_000_000_123,
                ["5.6.7.8"] = 1_760_000_001_234,
            },
        });

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraSessionRadiusSyncService>(RadiusType.Xui);

        var connections = await service.GetActiveConnections(Server, "User3");

        connections.Should().HaveCount(2);

        foreach (var connection in connections)
        {
            connection.Username.Should().Be("User3");
            connection.NasIpAddress.Should().Be(Server.IpAddress);
            connection.State.Should().Be(ConnectionState.Up);
            connection.UpTime.Should().Be(TimeSpan.Zero);
        }

        connections.Select(connection => connection.CallerId)
            .Should().BeEquivalentTo(["1.2.3.4", "5.6.7.8"]);

        connections.Select(connection => connection.SessionId)
            .Should().BeEquivalentTo(["xui:1.2.3.4", "xui:5.6.7.8"]);
    }

    [Fact]
    public async Task GetActiveConnections_UnknownUsername_ReturnsEmpty()
    {
        using var scope = App.Services.CreateScope();

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();

        api_moq.OnlineClients.Add(new XuiOnlineClientModel
        {
            Email = "SomeoneElse",
            Ips = new Dictionary<string, long> { ["1.2.3.4"] = 1 },
        });

        var service = scope.ServiceProvider.GetRequiredKeyedService<IInfraSessionRadiusSyncService>(RadiusType.Xui);

        var connections = await service.GetActiveConnections(Server, "User3");

        connections.Should().BeEmpty();
    }
}
