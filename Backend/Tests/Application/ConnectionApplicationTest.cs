using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Connection;
using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class ConnectionApplicationTest : ServiceInitializer
{
    protected override void AddServices(HostApplicationBuilder builder)
    {
        var price_repo = new Mock<IRadAcctRepository>();
        price_repo.Setup(x => x.GetCurrentConnectionList(It.IsNotNull<string>()))
            .Returns(Task.FromResult(RadAcctData));
        builder.Services.AddLazyScoped(_ => price_repo.Object);

        var nas_repo = new Mock<INasRepository>();
        nas_repo.Setup(x => x.GetNasInfo(It.IsNotNull<IEnumerable<string>>()))
            .Returns(Task.FromResult(NasData));
        builder.Services.AddLazyScoped(_ => nas_repo.Object);

        var vpn_nod_srv = new Mock<IVpnNodeService>();
        vpn_nod_srv.Setup(x => x.GetActiveConnections(It.IsNotNull<NasEntity>(), It.IsNotNull<string>()))
            .Returns<NasEntity, string>((s, _) =>
            {
                if (!NodeData.TryGetValue(s.IpAddress, out var data))
                {
                    data = [];
                }

                return Task.FromResult((s, data));
            });
        builder.Services.AddLazyScoped(_ => vpn_nod_srv.Object);
    }

    [Fact]
    public async Task GetCurrentConnectionState_CheckMerge()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var result = (await connection_app.GetCurrentConnectionState(string.Empty)).Data;

        Assert.NotNull(result);
        Assert.Equal(4, result.Count);

        var rad_data = RadAcctData.ToDictionary(k => k.AcctSessionId);
        var node_data = NodeData.SelectMany(m => m.Value).ToDictionary(k => k.SessionId);

        foreach (var connection in result)
        {
            Assert.True(rad_data.TryGetValue(connection.SessionId, out var rad));
            Assert.Equal(connection.Server, rad.NasIPAddress);

            Assert.Equal(
                node_data.ContainsKey(connection.SessionId) ? ConnectionState.Up : ConnectionState.Down,
                connection.State);
        }
    }

    private static readonly IList<RadAcctEntity> RadAcctData =
    [
        new()
        {
            NasIPAddress = "192.168.0.1",
            AcctStartTime = DateTime.Now.AddMinutes(-60),
            AcctSessionId = "0x1020",
        },
        new()
        {
            NasIPAddress = "192.168.0.2",
            AcctStartTime = DateTime.Now.AddMinutes(-45),
            AcctSessionId = "0x1021",
        },
        new()
        {
            NasIPAddress = "192.168.0.1",
            AcctStartTime = DateTime.Now.AddMinutes(-32),
            AcctSessionId = "0x1022",
        },
        new()
        {
            NasIPAddress = "192.168.0.3",
            AcctStartTime = DateTime.Now.AddMinutes(-61),
            AcctSessionId = "0x1023",
        },
    ];

    private static readonly Dictionary<string, NasEntity> NasData = new()
    {
        { "192.168.0.1", new NasEntity { IpAddress = "192.168.0.1" } },
        { "192.168.0.2", new NasEntity { IpAddress = "192.168.0.2" } },
        { "192.168.0.3", new NasEntity { IpAddress = "192.168.0.3" } },
    };

    private static readonly Dictionary<string, IList<UserConnectionBinding>> NodeData = new()
    {
        {
            "192.168.0.1",
            [new UserConnectionBinding { SessionId = "0x1020" }, new UserConnectionBinding { SessionId = "0x1025" }]
        },
        {
            "192.168.0.5",
            [new UserConnectionBinding { SessionId = "0x1031" }, new UserConnectionBinding { SessionId = "0x1030" }]
        },
        {
            "192.168.0.3",
            [new UserConnectionBinding { SessionId = "0x1023" }, new UserConnectionBinding { SessionId = "0x1050" }]
        },
    };
}