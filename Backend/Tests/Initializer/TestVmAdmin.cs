using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Tunnel;

namespace PhotonBypass.Test.Initializer;

/// <summary>
/// Out-of-band access to a test VM, for fixture work around the panel code under
/// test: the provisioning user with its key on Xui-Test (sudo without a password),
/// <c>admin</c> with its password on the routers. Returns the raw result of one
/// command; RouterOS stops a <c>;</c>-joined line at its first error, so send it one
/// command per call.
/// </summary>
internal static class TestVmAdmin
{
    private static readonly ServerBridgeOptions Defaults = new();

    public static async Task<SshCommandResult> Run(string vmKey, string command, CancellationToken ct = default)
    {
        var vm = TestVmOptions.Get(vmKey);

        // the VM is a local throw-away: its host key is pinned where the panel code is under test, not here
        using var channel = await new SshConnector().ConnectAsync(Endpoint(vmKey, vm), ct);
        using var commands = await channel.OpenCommandChannelAsync(ct);

        return commands.Execute(command, TimeSpan.FromSeconds(Defaults.SshCommandTimeoutSeconds));
    }

    private static SshEndpoint Endpoint(string vmKey, TestVmEndpoint vm)
    {
        var by_key = vm.AdminKeyFile != null;

        return new SshEndpoint(
            Host: vm.Ip,
            Port: vm.SshPort,
            Username: by_key ? vm.AdminUser ?? throw NoAdminUser(vmKey) : vm.Username,
            Password: by_key ? null : vm.Password,
            PrivateKey: by_key ? TestVmOptions.ReadFixtureFile(vmKey, vm.AdminKeyFile, "provisioning key") : null,
            PassPhrase: null,
            HostKeySha256: null,
            HostKeyPolicy: HostKeyPolicy.Optional,
            ConnectTimeout: TimeSpan.FromSeconds(Defaults.SshConnectTimeoutSeconds),
            KeepAlive: TimeSpan.FromSeconds(Defaults.SshKeepAliveIntervalSeconds));
    }

    private static Exception NoAdminUser(string vmKey) =>
        new($"The provisioning user of test VM '{vmKey}' is not configured — add AdminUser next to AdminKeyFile in Backend/Tests/appsettings.json.");
}
