using System.Text.Json;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Initializer;

/// <summary>
/// The server rows of the local test VMs (Docs/25 §5–§6, Docs/23 §4). The
/// <c>SshTunnel</c> row takes its SSH leg from the tunnel user, the key and the
/// host-key pin the VM scripts keep outside the repo (under
/// <see cref="TestVmOptions.KeyDirectory"/>), and addresses the management
/// service on the VM's own loopback — the shape the tunnel rules of
/// <c>ServerCrudApplication.ValidateConfiguration</c> accept. The
/// <c>Direct</c> row is its counterpart on the old transport.
/// </summary>
internal static class TestVmTunnel
{
    public const string LoopbackAddress = "127.0.0.1";

    /// <summary>Id of the x-ui test server: positive, because the pool keys by it and rejects unpersisted rows.</summary>
    public const int XuiServerId = 910001;

    public const int MikrotikServerId = 920001;

    /// <param name="pin">Overrides the recorded host-key pin — for the wrong-pin scenario.</param>
    /// <param name="keyText">Overrides the tunnel key — for the unauthorized-key scenario.</param>
    public static ServerEntity Server(
        string vmKey, int id, ServerFeature features, WebApiConfig? web = null, string? pin = null, string? keyText = null)
    {
        if ((features & (ServerFeature.Xui | ServerFeature.UserManager)) == 0)
        {
            throw new ArgumentException(
                "A tunnel test server carries the Xui or the UserManager feature: only those build a forward step.", nameof(features));
        }

        var vm = TestVmOptions.Get(vmKey);

        return Build(vmKey, id, features, new ServerConfiguration
        {
            Transport = ServerTransport.SshTunnel,
            SshConfig = new SshConfig
            {
                Username = vm.TunnelUser ?? throw NoTunnelUser(vmKey),
                Port = vm.SshPort,
                PrivateKey = (keyText ?? TunnelKey(vmKey)).Trim(),
                HostKeySha256 = SshTunnelConfig.NormalizeHostKeyPin(pin ?? Pin(vmKey)),
            },
            WebApiConfig = web ?? ServiceAddress(vmKey, features),
        });
    }

    /// <summary>The same VM on the <c>Direct</c> transport: the provisioning user with its password, no pin.</summary>
    public static ServerEntity DirectServer(string vmKey, int id, ServerFeature features)
    {
        var vm = TestVmOptions.Get(vmKey);

        return Build(vmKey, id, features, new ServerConfiguration
        {
            Transport = ServerTransport.Direct,
            SshConfig = new SshConfig { Username = vm.Username, Password = vm.Password, Port = vm.SshPort },
            WebApiConfig = new WebApiConfig
            {
                HostName = vm.Ip,
                Port = vm.Port,
                Ssl = false,
                Username = vm.Username,
                Password = vm.Password,
                InboundId = vm.InboundId,
            },
        });
    }

    /// <summary>The service address on the VM's own loopback: the panel login for an x-ui host, the API user for a router.</summary>
    public static WebApiConfig ServiceAddress(string vmKey, ServerFeature features)
    {
        var vm = TestVmOptions.Get(vmKey);
        var router = TestVmOptions.IsRouter(vmKey);

        return new WebApiConfig
        {
            HostName = LoopbackAddress,
            Port = vm.Port,
            Ssl = false,
            Username = router ? vm.TunnelUser ?? throw NoTunnelUser(vmKey) : vm.Username,
            Password = router ? vm.TunnelPassword : vm.Password,
            InboundId = router ? null : vm.InboundId,
        };
    }

    public static string TunnelKey(string vmKey) =>
        TestVmOptions.ReadFixtureFile(vmKey, TestVmOptions.Get(vmKey).TunnelKeyFile, "tunnel key");

    /// <summary>The host-key pin recorded by the VM script, in its normalized shape.</summary>
    public static string Pin(string vmKey) =>
        SshTunnelConfig.NormalizeHostKeyPin(TestVmOptions.ReadFixtureFile(vmKey, TestVmOptions.Get(vmKey).HostKeyFile, "host-key pin"));

    private static ServerEntity Build(string vmKey, int id, ServerFeature features, ServerConfiguration configuration)
    {
        return new ServerEntity
        {
            Id = id,
            Name = vmKey,
            IpAddress = TestVmOptions.Get(vmKey).Ip,
            DomainName = "tunnel.test.local",
            // RouterOS reports command failures as text on stdout: the verdict looks for it only on routers
            OsType = TestVmOptions.IsRouter(vmKey) ? OperatingSystem.Mikrotik : OperatingSystem.Ubuntu,
            Features = features,
            // JsonConfig is set directly (plaintext): parallel test classes swap ServerConfigProtection's
            // static delegates, and the plaintext pass-through of Unprotect is immune to that race
            JsonConfig = JsonSerializer.Serialize(configuration),
        };
    }

    private static Exception NoTunnelUser(string vmKey) =>
        new($"The tunnel user of test VM '{vmKey}' is not configured — add TunnelUser to its TestVmOptions entry in Backend/Tests/appsettings.json.");
}
