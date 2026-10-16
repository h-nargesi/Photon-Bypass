using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockXuiPanel;
using PhotonBypass.Xui.Application;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Fixture-driven tests for the vless link builder (docs/16 D4): the stream
/// settings below mirror the alireza0/x-ui v1.14.0 inbound payload shapes,
/// where `streamSettings` is a nested JSON string. The parser accepts both the
/// camelCase keys of the panel payload and the PascalCase variants seen in
/// exported configs.
/// </summary>
public class VlessLinkServiceTest : UnitLevelServiceInitializer
{
    private const string ClientUuid = "4c1f3b2a-1111-2222-3333-444455556666";

    private static readonly ServerEntity Server = new()
    {
        Id = 50,
        IsActive = true,
        RealmId = 2,
        Name = "XuiServer",
        IpAddress = "10.0.0.50",
        DomainName = "vm06.ir.parssafe",
        Features = ServerFeature.Xui | ServerFeature.V2ray,
    };

    [Fact]
    public void Build_WsTls_ProducesFullShareLink()
    {
        var inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 443,
            Protocol = "vless",
            StreamSettings = """
                {"network":"ws","security":"tls",
                 "tlsSettings":{"serverName":"vm06.ir.parssafe","fingerprint":"chrome","alpn":["h2","http/1.1"]},
                 "wsSettings":{"path":"/vpnv","headers":{"Host":"vm06.ir.parssafe"}}}
                """,
        };

        var link = VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        link.Should().Be(
            $"vless://{ClientUuid}@vm06.ir.parssafe:443" +
            "?encryption=none&type=ws&security=tls&sni=vm06.ir.parssafe&fp=chrome" +
            "&path=%2Fvpnv&host=vm06.ir.parssafe#User3");
    }

    [Fact]
    public void Build_TcpReality_CarriesRealityKeyMaterial()
    {
        var inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 8443,
            Protocol = "vless",
            StreamSettings = """
                {"network":"tcp","security":"reality",
                 "realitySettings":{"serverName":"www.example.com","fingerprint":"chrome",
                                    "publicKey":"pbk-value","shortId":"sid-value","spiderX":"/"}}
                """,
        };

        var link = VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        link.Should().Be(
            $"vless://{ClientUuid}@vm06.ir.parssafe:8443" +
            "?encryption=none&type=tcp&security=reality" +
            "&sni=www.example.com&fp=chrome&pbk=pbk-value&sid=sid-value#User3");
    }

    [Fact]
    public void Build_GrpcTls_EncodesServiceName()
    {
        var inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 443,
            Protocol = "vless",
            StreamSettings = """
                {"network":"grpc","security":"tls",
                 "tlsSettings":{"serverName":"vm06.ir.parssafe","fingerprint":"firefox"},
                 "grpcSettings":{"serviceName":"vpn grpc/service"}}
                """,
        };

        var link = VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        link.Should().Be(
            $"vless://{ClientUuid}@vm06.ir.parssafe:443" +
            "?encryption=none&type=grpc&security=tls&sni=vm06.ir.parssafe&fp=firefox" +
            "&serviceName=vpn%20grpc%2Fservice#User3");
    }

    [Fact]
    public void Build_PascalCaseStreamSettings_IsAccepted()
    {
        var inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 2053,
            Protocol = "vless",
            StreamSettings =
                """{"Network":"ws","Security":"tls","TlsSettings":{"ServerName":"s.example","Fingerprint":"chrome"},"WsSettings":{"Path":"/p","Headers":{"Host":"h.example"}}}""",
        };

        var link = VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        link.Should().Contain("type=ws&security=tls&sni=s.example&fp=chrome&path=%2Fp&host=h.example");
    }

    [Fact]
    public void Build_WithoutStreamSettings_FallsBackToPlainTcp()
    {
        var inbound = new XuiInboundModel { Id = 3, Port = 1080, Protocol = "vless", StreamSettings = null };

        var link = VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        link.Should().Be($"vless://{ClientUuid}@vm06.ir.parssafe:1080?encryption=none&type=tcp&security=none#User3");
    }

    [Fact]
    public void Build_IpAddressIsUsedWhenDomainIsMissing()
    {
        var server = new ServerEntity { Id = 51, RealmId = 2, Name = "XuiServer", IpAddress = "10.0.0.51", DomainName = "" };
        var inbound = new XuiInboundModel { Id = 3, Port = 443, Protocol = "vless" };

        var link = VlessLinkService.Build(server, inbound, ClientUuid, "User3");

        link.Should().StartWith($"vless://{ClientUuid}@10.0.0.51:443?");
    }

    [Fact]
    public void Build_NonVlessProtocol_FailsLoud()
    {
        var inbound = new XuiInboundModel { Id = 3, Port = 443, Protocol = "trojan" };

        var action = () => VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        action.Should().Throw<Exception>().WithMessage("*not a vless inbound*");
    }

    [Fact]
    public void Build_UnsupportedTransport_FailsLoud()
    {
        var inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 443,
            Protocol = "vless",
            StreamSettings = """{"network":"httpupgrade","security":"none"}""",
        };

        var action = () => VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        action.Should().Throw<Exception>().WithMessage("*network is not supported*");
    }

    [Fact]
    public void Build_UnsupportedSecurity_FailsLoud()
    {
        var inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 443,
            Protocol = "vless",
            StreamSettings = """{"network":"tcp","security":"xtls"}""",
        };

        var action = () => VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        action.Should().Throw<Exception>().WithMessage("*security is not supported*");
    }

    [Fact]
    public void Build_RealityWithMissingKeyMaterial_OmitsOnlyTheMissingParameters()
    {
        var inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 443,
            Protocol = "vless",
            StreamSettings = """
                {"network":"tcp","security":"reality",
                 "realitySettings":{"serverName":"s.example","publicKey":"pbk-value"}}
                """,
        };

        var link = VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        link.Should().Contain("security=reality&sni=s.example&pbk=pbk-value");
        link.Should().NotContain("sid=");
        link.Should().NotContain("fp=");
    }

    [Fact]
    public void Build_InvalidPort_FailsLoud()
    {
        var inbound = new XuiInboundModel { Id = 3, Port = 0, Protocol = "vless" };

        var action = () => VlessLinkService.Build(Server, inbound, ClientUuid, "User3");

        action.Should().Throw<Exception>().WithMessage("*invalid port*");
    }

    [Fact]
    public async Task GetVlessLink_MissingPanelClient_Throws()
    {
        using var scope = App.Services.CreateScope();

        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(2))
            .ReturnsAsync([Server]);

        var service = scope.ServiceProvider.GetRequiredService<IVpnLinkService>();

        var action = () => service.GetVlessLink(2, "User3");

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*The x-ui client was not found*");
    }

    [Fact]
    public async Task GetVlessLink_ReadsTheLiveClientAndInboundFromThePanel()
    {
        using var scope = App.Services.CreateScope();

        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(2))
            .ReturnsAsync([Server]);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User3", "drifted-live-uuid", 0, 0);
        api_moq.Inbound = new XuiInboundModel
        {
            Id = 3,
            Port = 443,
            Protocol = "vless",
            StreamSettings = """{"network":"ws","security":"tls","tlsSettings":{"serverName":"vm06.ir.parssafe"},"wsSettings":{"path":"/vpnv"}}""",
        };

        var service = scope.ServiceProvider.GetRequiredService<IVpnLinkService>();

        var link = await service.GetVlessLink(2, "User3");

        // keyed by the UUID currently live in the panel, not the database copy
        link.Should().StartWith("vless://drifted-live-uuid@vm06.ir.parssafe:443?");
        link.Should().Contain("type=ws&security=tls&sni=vm06.ir.parssafe&path=%2Fvpnv");
    }

    [Fact]
    public async Task GetVlessLink_WithoutXuiServer_Throws()
    {
        using var scope = App.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IVpnLinkService>();

        // realm 2 has no x-ui server in the seed
        var action = () => service.GetVlessLink(2, "User3");

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*No x-ui server found*");
    }
}
