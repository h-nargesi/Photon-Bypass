using Microsoft.Extensions.Configuration;

namespace PhotonBypass.Test.Initializer;

internal sealed record TestVmEndpoint
{
    public string Ip { get; init; } = "";

    public string Username { get; init; } = "admin";

    public string Password { get; init; } = "admin";

    public int? Port { get; init; }

    public int? InboundId { get; init; }

    /// <summary>SSH port of the VM (default 22).</summary>
    public int SshPort { get; init; } = 22;

    /// <summary>
    /// User of the <c>SshTunnel</c> fixture (Docs/25 §5): authenticated with
    /// <see cref="TunnelKeyFile"/>. On a router its password is the API password.
    /// </summary>
    public string? TunnelUser { get; init; }

    public string? TunnelPassword { get; init; }

    /// <summary>Private key of <see cref="TunnelUser"/>; absolute, or relative to <see cref="TestVmOptions.KeyDirectory"/>.</summary>
    public string? TunnelKeyFile { get; init; }

    /// <summary>Text file holding the pinned host key (<c>SHA256:…</c>); absolute, or relative to <see cref="TestVmOptions.KeyDirectory"/>.</summary>
    public string? HostKeyFile { get; init; }

    /// <summary>
    /// Provisioning identity for out-of-band work on the VM (<see cref="TestVmAdmin"/>):
    /// a user authenticated with <see cref="AdminKeyFile"/>. Null on routers, where
    /// <see cref="Username"/>/<see cref="Password"/> are used.
    /// </summary>
    public string? AdminUser { get; init; }

    public string? AdminKeyFile { get; init; }
}

/// <summary>
/// Lazy static accessor for the "TestVmOptions" section of Backend/Tests/appsettings.json
/// plus environment overrides (e.g. TestVmOptions__Mikrotik-Base__Ip). Static on purpose:
/// test fixtures read VM endpoints from static field initializers, before any DI container
/// exists. <see cref="Defaults"/> keeps today's hardcoded endpoints as fallback so the
/// current VMs keep working with an empty or partial section.
/// </summary>
internal static class TestVmOptions
{
    private const string SectionName = "TestVmOptions";

    private static readonly Lazy<IConfigurationRoot> Configuration = new(BuildConfiguration);

    private static readonly Dictionary<string, TestVmEndpoint> Defaults = new()
    {
        ["Mikrotik-Base"] = new() { Ip = "192.168.56.11", Port = 8728 },
        ["Mikrotik-Scenario"] = new() { Ip = "192.168.56.12", Port = 8728 },
        // InboundId 1 = the id the factory script reads back on a fresh panel
        // (first inbound; Scripts/xui-test-vm.sh, live-validated — Docs/23 s4.1)
        ["Xui-Test"] = new() { Ip = "192.168.56.13", Port = 2087, Password = "xui.test.123456", InboundId = 1 },
    };

    /// <summary>
    /// Directory of the test keys and host-key pins — the same directory the VM
    /// scripts write to: <c>TestVmOptions:KeyDirectory</c>, else
    /// <c>${XDG_DATA_HOME:-~/.local/share}/photon-bypass</c>. Outside the repo on
    /// purpose (Docs/25 §6).
    /// </summary>
    public static string KeyDirectory { get; } =
        Configuration.Value[$"{SectionName}:KeyDirectory"] ?? DefaultKeyDirectory();

    public static TestVmEndpoint Get(string key)
    {
        var section = Configuration.Value.GetSection($"{SectionName}:{key}");
        var endpoint = Defaults.TryGetValue(key, out var fallback) ? fallback : new TestVmEndpoint();

        endpoint = endpoint with
        {
            Ip = section["Ip"] ?? endpoint.Ip,
            Username = section["Username"] ?? endpoint.Username,
            Password = section["Password"] ?? endpoint.Password,
            Port = ReadInt(section, "Port") ?? ReadInt(section, "ApiPort") ?? endpoint.Port,
            InboundId = ReadInt(section, "InboundId") ?? endpoint.InboundId,
            SshPort = ReadInt(section, "SshPort") ?? endpoint.SshPort,
            TunnelUser = section["TunnelUser"] ?? endpoint.TunnelUser,
            TunnelPassword = section["TunnelPassword"] ?? endpoint.TunnelPassword,
            TunnelKeyFile = section["TunnelKeyFile"] ?? endpoint.TunnelKeyFile,
            HostKeyFile = section["HostKeyFile"] ?? endpoint.HostKeyFile,
            AdminUser = section["AdminUser"] ?? endpoint.AdminUser,
            AdminKeyFile = section["AdminKeyFile"] ?? endpoint.AdminKeyFile,
        };

        return string.IsNullOrWhiteSpace(endpoint.Ip)
            ? throw new Exception(
                $"No IP is configured for test VM '{key}' — add it to the TestVmOptions section of Backend/Tests/appsettings.json.")
            : endpoint;
    }

    /// <summary>Resolves a key or pin file of the configured directory: absolute paths are taken as they are.</summary>
    public static string ResolveFile(string file) =>
        Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(KeyDirectory, file));

    /// <summary>
    /// Reads a fixture file of the VM (a tunnel key or a host-key pin). A missing file is
    /// a hard failure that names the script stage producing it — there is no skip.
    /// </summary>
    public static string ReadFixtureFile(string vmKey, string? file, string what)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            throw new Exception($"The {what} file of test VM '{vmKey}' is not configured — add it to its TestVmOptions entry in Backend/Tests/appsettings.json.");
        }

        var path = ResolveFile(file);

        return File.Exists(path)
            ? File.ReadAllText(path)
            : throw new Exception($"The {what} file '{path}' of test VM '{vmKey}' was not found — run `{ProvisionCommand(vmKey)}` first.");
    }

    /// <summary>The script stage that installs (or restores) the tunnel fixture of the VM.</summary>
    public static string ProvisionCommand(string vmKey) => IsRouter(vmKey)
        ? $"Scripts/mikrotik-test-vms.sh provision {vmKey["Mikrotik-".Length..].ToLowerInvariant()}"
        : "Scripts/xui-test-vm.sh provision";

    /// <summary>VMs named <c>Mikrotik-*</c> are RouterOS routers; the others are Debian hosts.</summary>
    public static bool IsRouter(string vmKey) => vmKey.StartsWith("Mikrotik-", StringComparison.OrdinalIgnoreCase);

    private static string DefaultKeyDirectory()
    {
        var data_home = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        if (string.IsNullOrWhiteSpace(data_home))
        {
            data_home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(data_home, "photon-bypass");
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static int? ReadInt(IConfiguration section, string key)
    {
        return int.TryParse(section[key], out var parsed) ? parsed : null;
    }
}
