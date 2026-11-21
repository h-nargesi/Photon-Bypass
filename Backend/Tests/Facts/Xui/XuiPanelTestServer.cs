using System.Text.Json;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Shared wiring for the Xui-Test VM (Docs/23): the in-memory server entity
/// for direct <see cref="PhotonBypass.Xui.WebService.IXuiApi"/> calls, the
/// plaintext-JsonConfig variant for DB-persisted rows, and per-test unique
/// client emails (also used as VPN usernames: globally unique, &lt;= 32 chars,
/// dotted-lowercase — the D3 shape).
/// </summary>
internal static class XuiPanelTestServer
{
    public static readonly TestVmEndpoint Vm = TestVmOptions.Get(XuiPanelInitializer.VmKey);

    public static ServerEntity Build()
    {
        return new ServerEntity
        {
            Name = XuiPanelInitializer.VmKey,
            IpAddress = Vm.Ip,
            DomainName = "xui.test.local",
            Config = new ServerConfiguration
            {
                WebApiConfig = BuildWebApiConfig(),
            },
        };
    }

    public static ServerEntity BuildDbRow(int realm_id)
    {
        // JsonConfig is set directly (plaintext) instead of via the Config
        // property: parallel test classes swap ServerConfigProtection's static
        // delegates, and the plaintext pass-through of Unprotect is immune to
        // that race (same rationale as XuiApiTransportTest.BuildServer).
        return new ServerEntity
        {
            IsActive = true,
            RealmId = realm_id,
            Name = "Xui-Live",
            IpAddress = Vm.Ip,
            DomainName = "xui.test.local",
            BandWidth = 10_737_418_240,
            OsType = Domain.Servers.Types.OperatingSystem.Ubuntu,
            Features = Domain.Servers.Types.ServerFeature.Xui | Domain.Servers.Types.ServerFeature.V2ray,
            JsonConfig = JsonSerializer.Serialize(new ServerConfiguration
            {
                WebApiConfig = BuildWebApiConfig(),
            }),
        };
    }

    private static WebApiConfig BuildWebApiConfig()
    {
        return new WebApiConfig
        {
            HostName = Vm.Ip,
            Port = Vm.Port,
            Username = Vm.Username,
            Password = Vm.Password,
            InboundId = Vm.InboundId,
            Ssl = false,
        };
    }

    public static string Email(string scope)
    {
        return $"it.{scope}.{Guid.NewGuid().ToString("N")[..8]}";
    }
}
