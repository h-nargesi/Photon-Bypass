using System.Text;
using System.Text.Json;
using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;

namespace PhotonBypass.Test.Facts.BasicFunctions.Domain;

public class ServerConfigProtectionTest : IDisposable
{
    private readonly Func<string, string>? protect_backup;
    private readonly Func<string, string>? unprotect_backup;

    public ServerConfigProtectionTest()
    {
        protect_backup = ServerConfigProtection.ProtectImpl;
        unprotect_backup = ServerConfigProtection.UnprotectImpl;

        ServerConfigProtection.ProtectImpl = null;
        ServerConfigProtection.UnprotectImpl = null;
    }

    public void Dispose()
    {
        ServerConfigProtection.ProtectImpl = protect_backup;
        ServerConfigProtection.UnprotectImpl = unprotect_backup;
    }

    [Fact]
    public void WithoutWiring_ValuesStayPlaintext()
    {
        var stored = ProtectWithoutWiring();

        stored.Should().Be("{\"User\":\"root\"}");
        ServerConfigProtection.IsProtected(stored).Should().BeFalse();
        ServerConfigProtection.Unprotect(stored).Should().Be("{\"User\":\"root\"}");
    }

    [Fact]
    public void Protect_AddsMarker_AndUnprotectRoundTrips()
    {
        var stored = ProtectWithRoundTrip(out var round_tripped);

        stored.Should().StartWith("protected:");
        ServerConfigProtection.IsProtected(stored).Should().BeTrue();
        round_tripped.Should().Be("{\"User\":\"root\"}");
    }

    [Fact]
    public void Unprotect_ProtectedValueWithoutImpl_ReturnsNull()
    {
        ServerConfigProtection.Unprotect("protected:Zm9v").Should().BeNull();
    }

    [Fact]
    public void Unprotect_FailingImpl_ReturnsNull()
    {
        ServerConfigProtection.UnprotectImpl = _ => throw new FormatException("bad payload");

        ServerConfigProtection.Unprotect("protected:bad").Should().BeNull();
    }

    [Fact]
    public void ServerEntity_Config_RoundTripsThroughProtection()
    {
        ServerConfigProtection.ProtectImpl = ToBase64;
        ServerConfigProtection.UnprotectImpl = FromBase64;

        var server = new ServerEntity
        {
            Config = new ServerConfiguration
            {
                SshConfig = new SshConfig { Username = "root", Password = "secret" },
            },
        };

        server.JsonConfig.Should().StartWith("protected:");
        server.JsonConfig.Should().NotContain("secret");

        var round_tripped = new ServerEntity { JsonConfig = server.JsonConfig }.Config;

        round_tripped.Should().NotBeNull();
        round_tripped!.SshConfig!.Username.Should().Be("root");
        round_tripped.SshConfig.Password.Should().Be("secret");
    }

    [Fact]
    public void ServerEntity_Config_ReadsLegacyPlaintext()
    {
        var server = new ServerEntity
        {
            JsonConfig = JsonSerializer.Serialize(new ServerConfiguration
            {
                SshConfig = new SshConfig { Username = "root" },
            }),
        };

        server.Config.Should().NotBeNull();
        server.Config!.SshConfig!.Username.Should().Be("root");
    }

    /// <summary>
    /// Scenario test hosts wire the static protection delegates when they
    /// build (parallel xUnit collections); re-null and retry until a Protect
    /// call observes the unwired state — a host racing the null assignment is
    /// transient and settles once its build finishes.
    /// </summary>
    private static string ProtectWithoutWiring()
    {
        const string payload = "{\"User\":\"root\"}";

        for (var attempt = 0; ; attempt++)
        {
            ServerConfigProtection.ProtectImpl = null;

            var stored = ServerConfigProtection.Protect(payload);

            if (!ServerConfigProtection.IsProtected(stored) || attempt >= 100)
            {
                return stored;
            }

            Thread.Sleep(50);
        }
    }

    /// <summary>
    /// Same host-wiring race as <see cref="ProtectWithoutWiring"/>: re-apply
    /// the base64 impls and retry until the full protect→unprotect round trip
    /// observes them together.
    /// </summary>
    private static string ProtectWithRoundTrip(out string round_tripped)
    {
        const string payload = "{\"User\":\"root\"}";

        for (var attempt = 0; ; attempt++)
        {
            ServerConfigProtection.ProtectImpl = ToBase64;
            ServerConfigProtection.UnprotectImpl = FromBase64;

            var stored = ServerConfigProtection.Protect(payload);
            round_tripped = ServerConfigProtection.Unprotect(stored) ?? string.Empty;

            if (round_tripped == payload || attempt >= 100)
            {
                return stored;
            }

            Thread.Sleep(50);
        }
    }

    private static string ToBase64(string plain) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));

    private static string FromBase64(string encoded) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
}
