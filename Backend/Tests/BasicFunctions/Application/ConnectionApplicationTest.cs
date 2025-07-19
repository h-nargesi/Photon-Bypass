using Moq;
using PhotonBypass.Application.Connection;
using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.BasicFunctions.Application;

public class ConnectionApplicationTest : ServiceInitializer
{
    public ConnectionApplicationTest()
    {
        var builder = Initialize();

        var priceRepo = new Mock<IRadAcctRepository>();
        priceRepo.Setup(x => x.GetCurrentConnectionList(It.IsNotNull<string>()))
            .Returns(Task.FromResult(RadAcctData));
        builder.Services.AddLazyScoped(s => priceRepo.Object);

        var nasRepo = new Mock<INasRepository>();
        nasRepo.Setup(x => x.GetNasInfo(It.IsNotNull<IEnumerable<string>>()))
            .Returns(Task.FromResult(NasData));
        builder.Services.AddLazyScoped(s => nasRepo.Object);

        var vpnNodSrv = new Mock<IVpnNodeService>();
        vpnNodSrv.Setup(x => x.GetActiveConnections(It.IsNotNull<NasEntity>(), It.IsNotNull<string>()))
            .Returns<NasEntity, string>((s, _) =>
            {
                if (!NodeData.TryGetValue(s.IpAddress, out var data))
                {
                    data = [];

                }

                return Task.FromResult((s, data));
            });
        builder.Services.AddLazyScoped(s => vpnNodSrv.Object);

        Build(builder);
    }

    [Fact]
    public async Task GetCurrentConnectionState_CheckMerge()
    {
        var ConnectionApp = CreateScope().GetRequiredService<IConnectionApplication>();
        var result = (await ConnectionApp.GetCurrentConnectionState(string.Empty)).Data;

        Assert.NotNull(result);
        Assert.Equal(4, result.Count);

        var rad_data = RadAcctData.ToDictionary(k => k.AcctSessionId);
        var node_data= NodeData.SelectMany(m => m.Value).ToDictionary(k => k.SessionId);

        foreach (var connection in result)
        {
            Assert.True(rad_data.TryGetValue(connection.SessionId, out var rad));
            Assert.Equal(connection.Server, rad.NasIPAddress);

            if (node_data.TryGetValue(connection.SessionId, out var node))
            {
                Assert.Equal(ConnectionState.Up, connection.State);
            }
            else
            {
                Assert.Equal(ConnectionState.Down, connection.State);
            }
        }
    }

    readonly static IList<RadAcctEntity> RadAcctData =
    [
        new RadAcctEntity
        {
            NasIPAddress = "192.168.0.1",
            AcctStartTime = DateTime.Now.AddMinutes(-60),
            AcctSessionId = "0x1020",
        },
        new RadAcctEntity
        {
            NasIPAddress = "192.168.0.2",
            AcctStartTime = DateTime.Now.AddMinutes(-45),
            AcctSessionId = "0x1021",
        },
        new RadAcctEntity
        {
            NasIPAddress = "192.168.0.1",
            AcctStartTime = DateTime.Now.AddMinutes(-32),
            AcctSessionId = "0x1022",
        },
        new RadAcctEntity
        {
            NasIPAddress = "192.168.0.3",
            AcctStartTime = DateTime.Now.AddMinutes(-61),
            AcctSessionId = "0x1023",
        },
    ];

    readonly static Dictionary<string, NasEntity> NasData = new Dictionary<string, NasEntity>()
    {
        { "192.168.0.1", new NasEntity { IpAddress = "192.168.0.1" } },
        { "192.168.0.2", new NasEntity { IpAddress = "192.168.0.2" } },
        { "192.168.0.3", new NasEntity { IpAddress = "192.168.0.3" } },
    };

    readonly static Dictionary<string, IList<UserConnectionBinding>> NodeData = new()
    {
        { "192.168.0.1", [new UserConnectionBinding { SessionId = "0x1020" }, new UserConnectionBinding { SessionId = "0x1025" }] },
        { "192.168.0.5", [new UserConnectionBinding { SessionId = "0x1031" }, new UserConnectionBinding { SessionId = "0x1030" }] },
        { "192.168.0.3", [new UserConnectionBinding { SessionId = "0x1023" }, new UserConnectionBinding { SessionId = "0x1050" }] },
    };
}
