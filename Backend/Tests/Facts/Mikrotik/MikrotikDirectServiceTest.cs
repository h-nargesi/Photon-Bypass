using FluentAssertions;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Nas;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.Mikrotik;

public class MikrotikDirectServiceTest : UnitLevelServiceInitializer
{
    private const string Username = "user_1";

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        ConfigurableSshHandlerMoq.CreateInstance(builder.Services);
    }

    [Fact]
    public async Task GetOVpnCertificate_WhenCertificateIsMissing_GeneratesSignsAndExports()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMikrotikDirectService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        // certificate does not exist yet + stale exported files are present
        ssh.RespondToPrefix(@"/certificate print where name=""CLIENT_", " ");
        ssh.RespondToPrefix("/certificate add ", " ");
        ssh.RespondToPrefix("/certificate sign ", " ");
        ssh.RespondToPrefix(@"/file print where name=""CLIENT_user_1.crt""", "stale file");
        ssh.RespondToPrefix(@"/file print where name=""CLIENT_user_1.key""", "stale file");
        ssh.RespondToPrefix("/file remove ", " ");
        ssh.RespondToPrefix("/certificate export-certificate ", " ");
        ssh.RespondToPrefix(@":put [/file get ""CLIENT_user_1"".crt", "CERT-DATA");
        ssh.RespondToPrefix(@":put [/file get ""CLIENT_user_1"".key", "KEY-DATA");

        var context = NewCertContext();

        await service.GetOVpnCertificate(NewServer(), Username, context);

        ssh.Commands.Should().Equal(
            @"/certificate print where name=""CLIENT_user_1""",
            @"/certificate add name=""CLIENT_user_1"" copy-from=CLIENT-TEMPLATE common-name=""CLIENT_user_1""",
            @"/certificate sign ""CLIENT_user_1"" ca=LMTCA name=""CLIENT_user_1""",
            @"/file print where name=""CLIENT_user_1.crt""",
            @"/file remove ""CLIENT_user_1"".crt",
            @"/file print where name=""CLIENT_user_1.key""",
            @"/file remove ""CLIENT_user_1"".key",
            @"/certificate export-certificate ""CLIENT_user_1"" export-passphrase=""" + context.PrivateKeyOvpn + @""" file-name=""CLIENT_user_1""",
            @":put [/file get ""CLIENT_user_1"".crt contents]",
            @":put [/file get ""CLIENT_user_1"".key contents]");

        var ovpn_file = System.Text.Encoding.UTF8.GetString(context.CertFile);
        ovpn_file.Should().Contain("CERT-DATA");
        ovpn_file.Should().Contain("KEY-DATA");
        ovpn_file.Should().NotContain("placeholder");
        context.PrivateKeyOvpn.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetOVpnCertificate_WhenCertificateExists_SkipsGeneration()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMikrotikDirectService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        ssh.RespondToPrefix(@"/certificate print where name=""CLIENT_", "0 name=\"CLIENT_user_1\"");
        ssh.RespondToPrefix(@"/file print where name=""CLIENT_user_1.crt""", " ");
        ssh.RespondToPrefix(@"/file print where name=""CLIENT_user_1.key""", " ");
        ssh.RespondToPrefix("/certificate export-certificate ", " ");
        ssh.RespondToPrefix(@":put [/file get ""CLIENT_user_1"".crt", "CERT-DATA");
        ssh.RespondToPrefix(@":put [/file get ""CLIENT_user_1"".key", "KEY-DATA");

        var context = NewCertContext();

        await service.GetOVpnCertificate(NewServer(), Username, context);

        ssh.Commands.Should().NotContain(c => c.StartsWith("/certificate add") || c.StartsWith("/certificate sign"));
        ssh.Commands.Should().NotContain(c => c.StartsWith("/file remove"));
        ssh.Commands.Should().Contain(
            @"/certificate export-certificate ""CLIENT_user_1"" export-passphrase=""" + context.PrivateKeyOvpn + @""" file-name=""CLIENT_user_1""");
    }

    [Theory]
    [InlineData("user;reboot")]
    [InlineData("user\n")]
    [InlineData("کاربر")]
    public async Task GetOVpnCertificate_WithInvalidUsername_ThrowsBeforeAnySshCall(string username)
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMikrotikDirectService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        var context = NewCertContext();

        (await service.Invoking(s => s.GetOVpnCertificate(NewServer(), username, context))
                .Should().ThrowAsync<Exception>())
            .WithMessage($"Invalid username! ({username})");

        ssh.Connects.Should().BeEmpty();
        ssh.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOVpnCertificate_WhenCertificateCheckFails_SurfacesError()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMikrotikDirectService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.FailToPrefix(@"/certificate print where name=""CLIENT_", "nas unreachable");

        var context = NewCertContext();

        (await service.Invoking(s => s.GetOVpnCertificate(NewServer(), Username, context))
                .Should().ThrowAsync<Exception>())
            .WithMessage("Certificate check failed!");

        ssh.Commands.Should().ContainSingle();
    }

    private static ServerEntity NewServer() => new()
    {
        Name = "nas-server",
        IpAddress = "10.10.10.10",
        DomainName = "nas-server.local",
        RealmId = 1,
        OsType = OperatingSystem.Mikrotik,
    };

    private static CertContext NewCertContext() => new()
    {
        Realm = "realm-1",
        CertFile = System.Text.Encoding.UTF8.GetBytes("<cert>placeholder</cert>\n<key>placeholder</key>\n"),
    };
}
