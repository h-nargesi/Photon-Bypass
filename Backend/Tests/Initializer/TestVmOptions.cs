using Microsoft.Extensions.Configuration;

namespace PhotonBypass.Test.Initializer;

internal sealed record TestVmEndpoint
{
    public string Ip { get; init; } = "";

    public string Username { get; init; } = "admin";

    public string Password { get; init; } = "admin";

    public int? Port { get; init; }

    public int? InboundId { get; init; }
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
        ["Xui-Test"] = new() { Ip = "192.168.56.13", Port = 2087, Password = "xui.test.123456", InboundId = 0 },
    };

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
        };

        return string.IsNullOrWhiteSpace(endpoint.Ip)
            ? throw new Exception(
                $"No IP is configured for test VM '{key}' — add it to the TestVmOptions section of Backend/Tests/appsettings.json.")
            : endpoint;
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
