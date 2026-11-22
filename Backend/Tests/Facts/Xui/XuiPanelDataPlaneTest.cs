using FluentAssertions;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Live data-plane tier (Docs/23 §4.4): real VLESS traffic through the test
/// inbound moves the panel's per-client counters, and a connected client is
/// visible in <c>onlines</c>. Uses <see cref="VlessWsClient"/> — no extra
/// package, BCL websocket only.
/// </summary>
[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelDataPlaneTest : OutSourceLevelServiceInitializer
{
    private const int PaddingBytes = 300_000;

    [Fact]
    public async Task RealTraffic_GrowsCounters_UpAndDown()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("dp-count");
        var uuid = Guid.NewGuid().ToString();

        await api.Value.AddClient(server, new XuiClientModel { Email = email, Uuid = uuid });

        try
        {
            await XuiPanelTrafficProbe.PushTrafficAsync(api, server, email, PaddingBytes);

            await XuiPanelTrafficProbe.SettledTotalAsync(api, server, email, minimum: 100_000);

            var stats = await XuiPanelTrafficProbe.ReadStatsAsync(api, server, email);

            stats.Up.Should().BeGreaterThanOrEqualTo(100_000);
            stats.Down.Should().BeGreaterThan(0);
        }
        finally
        {
            await api.Value.RemoveClient(server, uuid);
        }
    }

    [Fact]
    public async Task Onlines_ConnectedClient_ListedWithEmailAndIp()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("dp-online");
        var uuid = Guid.NewGuid().ToString();

        await api.Value.AddClient(server, new XuiClientModel { Email = email, Uuid = uuid });

        // everything from here on is inside the try: a failure between the
        // add and the tunnel connect would otherwise leak the client into the
        // shared-collection state (the panel keeps it until the next run's
        // snapshot restore)
        VlessWsClient? tunnel = null;
        try
        {
            var inbound = await api.Value.GetInbound(server);
            var path = System.Text.Json.JsonDocument.Parse(inbound.StreamSettings!).RootElement
                .GetProperty("wsSettings").GetProperty("path").GetString()!;
            var host = XuiPanelTestServer.Vm.Ip;

            // sshd as the tunnel destination: it waits on the pre-auth version
            // line, so the tunnel stays open for the whole poll without the panel
            // closing an idle HTTP connection
            tunnel = await VlessWsClient.ConnectAsync(
                host, inbound.Port, path, uuid, host, 22);

            // the online set is refreshed asynchronously: keep the tunnel
            // minimally active while waiting for the panel to list it. The cap
            // accommodates slow virtualization hosts (e.g. VirtualBox NEM),
            // where the panel's online refresh can take minutes
            await XuiPanelTrafficProbe.PollUntilAsync(async () =>
            {
                await tunnel.NudgeAsync();
                return (await api.Value.ListOnlineClients(server)).Any(online => online.Email == email);
            }, $"the connected client '{email}' to appear in onlines", cap: TimeSpan.FromSeconds(300));

            var entry = (await api.Value.ListOnlineClients(server)).Single(online => online.Email == email);

            entry.Ips.Should().NotBeEmpty();
        }
        finally
        {
            if (tunnel is not null)
            {
                await tunnel.DisposeAsync();
            }
            await api.Value.RemoveClient(server, uuid);
        }
    }
}
