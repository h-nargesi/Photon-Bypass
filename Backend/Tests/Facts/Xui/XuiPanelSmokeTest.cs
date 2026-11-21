using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelSmokeTest : OutSourceLevelServiceInitializer
{
    private static readonly TestVmEndpoint Vm = TestVmOptions.Get(XuiPanelInitializer.VmKey);

    [Fact]
    public async Task PanelVm_ResetsAndAnswersTheRealApi()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        var server = new ServerEntity
        {
            Name = XuiPanelInitializer.VmKey,
            IpAddress = Vm.Ip,
            DomainName = "xui.test.local",
            Config = new ServerConfiguration
            {
                WebApiConfig = new WebApiConfig
                {
                    HostName = Vm.Ip,
                    Port = Vm.Port,
                    Username = Vm.Username,
                    Password = Vm.Password,
                    InboundId = Vm.InboundId,
                    Ssl = false,
                }
            }
        };

        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();

        var inbound = await api.Value.GetInbound(server);

        inbound.Id.Should().Be(Vm.InboundId);

        // the freshly restored snapshot has no clients; during a shared run the
        // suite's carrier (XuiPanelCarrier — the panel refuses deleting the
        // last client) is the only allowed occupant
        (await api.Value.ListClients(server))
            .Should().NotContain(client => client.Email != XuiPanelCarrier.Email);
    }
}
