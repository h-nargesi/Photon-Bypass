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
        var stored = ServerConfigProtection.Protect("{\"User\":\"root\"}");

        stored.Should().Be("{\"User\":\"root\"}");
        ServerConfigProtection.IsProtected(stored).Should().BeFalse();
        ServerConfigProtection.Unprotect(stored).Should().Be("{\"User\":\"root\"}");
    }

    [Fact]
    public void Protect_AddsMarker_AndUnprotectRoundTrips()
    {
        ServerConfigProtection.ProtectImpl = ToBase64;
        ServerConfigProtection.UnprotectImpl = FromBase64;

        var stored = ServerConfigProtection.Protect("{\"User\":\"root\"}");

        stored.Should().StartWith("protected:");
        ServerConfigProtection.IsProtected(stored).Should().BeTrue();
        ServerConfigProtection.Unprotect(stored).Should().Be("{\"User\":\"root\"}");
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

    private static string ToBase64(string plain) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));

    private static string FromBase64(string encoded) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
}
