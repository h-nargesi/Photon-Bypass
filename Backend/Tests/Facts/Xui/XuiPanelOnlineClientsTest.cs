using FluentAssertions;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Live onlines contract (Docs/17 §4, Docs/23 §4.4): an idle inbound reports
/// an empty online list. The connected-client variant lives in the data-plane
/// tier (XuiPanelDataPlaneTest) because it needs the VLESS fixture.
/// </summary>
[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelOnlineClientsTest : OutSourceLevelServiceInitializer
{
    [Fact]
    public async Task Onlines_NoConnectedClients_ReturnsEmpty()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = XuiPanelTestServer.Build();
        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();

        // poll instead of asserting immediately: a leaked connection from a
        // failed earlier test expires out of the online set within seconds
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < TimeSpan.FromSeconds(60))
        {
            if ((await api.Value.ListOnlineClients(server)).Count == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        (await api.Value.ListOnlineClients(server)).Should().BeEmpty();
    }
}
