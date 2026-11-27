using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.Application;

/// <summary>
/// Admin server CRUD contract (Docs/20 phase 5, D8): validation matrix
/// (ip/name/domain/capacity/max-clients/os/features/realm), the 409 duplicate
/// guard on identity fields, write-without-read config handling and the
/// admin-only defense in depth. The list never carries the stored config —
/// only scalars and the has-config flag.
/// </summary>
public class ServerCrudApplicationTest : UnitLevelServiceInitializer
{
    private const long OnePebibyte = 1L << 50;
    private static SaveServerModel Model(
        int id = 0,
        string ip = "10.20.30.40",
        string name = "SRV-Unit01",
        string domain = "unit01",
        int realm_id = 5,
        long capacity_bytes = 1_073_741_824,
        int? max_clients = null,
        OperatingSystem os = OperatingSystem.Mikrotik,
        ServerFeature features = ServerFeature.UserManager | ServerFeature.V2ray,
        ServerConfiguration? config = null)
    {
        return new SaveServerModel
        {
            Id = id,
            RealmId = realm_id,
            IpAddress = ip,
            Name = name,
            DomainName = domain,
            CapacityBytes = capacity_bytes,
            MaxClients = max_clients,
            OsType = os,
            Features = features,
            Config = config,
        };
    }

    [Fact]
    public async Task GetList_AdminOnly_MapsScalarsWithoutConfig()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        // defense in depth behind the Admin policy: a plain user is refused
        job_context.InjectJobContext(4, "User4", "User4");

        (await Assert.ThrowsAsync<UserException>(() => application.GetList()))
            .HttpCode.Should().Be(403);

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        var list = (await application.GetList()).Data!;

        Assert.Equal(server_moq.Data.Count, list.Count);

        var row = Assert.Single(list, r => r.Name == "Server1");

        Assert.Equal("192.168.125.11", row.IpAddress);
        Assert.Equal(1, row.RealmId);
        Assert.Equal("Realm-LO-1", row.Realm);
        Assert.False(row.HasConfig); // the fixture rows carry no config
        Assert.Equal(server_moq.Data[1].Features, row.Features);
        Assert.Null(row.MaxClients); // fixture rows run with the count gate off
    }

    [Fact]
    public async Task Create_ValidationMatrix()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(ip: "not-an-ip")));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(ip: "2001:db8::1"))); // IPv6 exceeds varchar(15)
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(name: "")));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(name: new string('n', 16))));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(domain: new string('d', 256))));
        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(capacity_bytes: 0))))
            .UserMessage.Should().Contain("ظرفیت ترافیک سرور نامعتبر");
        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(capacity_bytes: OnePebibyte + 1))))
            .UserMessage.Should().Contain("ظرفیت ترافیک سرور بیش از حد بزرگ"); // Docs/24 D4 unit guard
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(max_clients: 0))); // T4: null allowed, 0 is not
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(os: (OperatingSystem)99)));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(features: 0)));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(features: unchecked((ServerFeature)0x8000_0000))));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(realm_id: 999)));

        // happy path: created active with the config stored (protected or not,
        // the round-trip through the Config property must restore the payload)
        var config = new ServerConfiguration
        {
            SshConfig = new SshConfig { Username = "root", Password = "unit-secret", Port = 2222 },
            WebApiConfig = new WebApiConfig { HostName = "192.168.125.50", Username = "admin", Password = "api-secret" },
        };

        await application.Create(Model(name: "SRV-Ok01", domain: "ok01", config: config));

        var created = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Ok01");

        Assert.True(created.IsActive);
        Assert.Equal(5, created.RealmId);
        Assert.NotNull(created.JsonConfig);
        Assert.Equal("unit-secret", created.Config!.SshConfig!.Password);
        Assert.Equal(1_073_741_824, created.CapacityBytes);
    }

    [Fact]
    public async Task Create_DirectWithoutKeyAndPin_IsStillAccepted()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        await application.Create(Model(name: "SRV-Direct1", domain: "direct1", ip: "10.90.0.1", config: ChannelConfig()));

        var created = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Direct1");

        Assert.Equal(ServerTransport.Direct, created.Config!.Transport);
        Assert.Null(created.Config.SshConfig!.PrivateKey);
        Assert.Null(created.Config.SshConfig.HostKeySha256);
        created.Config.Transport.Should().Be(ServerTransport.Direct);
    }

    [Theory]
    [InlineData("username")]
    [InlineData("ssh-config")]
    [InlineData("key-empty")]
    [InlineData("key-rsa")]
    [InlineData("key-pkcs8")]
    [InlineData("pin-empty")]
    [InlineData("pin-short")]
    [InlineData("pin-prefix")]
    public async Task Create_TunnelWithoutASshLegToAuthenticateAndPin_IsRejected(string broken)
    {
        using var scope = App.Services.CreateScope();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel: true);
        var expected = broken switch
        {
            "username" or "ssh-config" => "برای حالت تانل، کاربر SSH الزامی است!",
            "pin-empty" or "pin-short" or "pin-prefix" => "کلید میزبان (host key) ثبت نشده یا نامعتبر است!",
            _ => "کلید خصوصی SSH معتبر نیست!",
        };

        switch (broken)
        {
            case "username": config.SshConfig!.Username = " "; break;
            case "ssh-config": config.SshConfig = null; break;
            case "key-empty": config.SshConfig!.PrivateKey = null; break;
            case "key-rsa": config.SshConfig!.PrivateKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIB\n-----END RSA PRIVATE KEY-----"; break;
            case "key-pkcs8": config.SshConfig!.PrivateKey = "-----BEGIN PRIVATE KEY-----\nMIIB\n-----END PRIVATE KEY-----"; break;
            case "pin-empty": config.SshConfig!.HostKeySha256 = null; break;
            case "pin-short": config.SshConfig!.HostKeySha256 = "SHA256:short"; break;
            case "pin-prefix": config.SshConfig!.HostKeySha256 = "AAAAB3NzaC1yc2E"; break;
        }

        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(config: config))))
            .UserMessage.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 70000)]
    [InlineData(false, 0)]
    [InlineData(false, 70000)]
    public async Task Create_SshPortOutOfRange_IsRejectedOnBothTransports(bool tunnel, int port)
    {
        using var scope = App.Services.CreateScope();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel);
        config.SshConfig!.Port = port;

        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(config: config))))
            .UserMessage.Should().Be("پورت SSH نامعتبر است!");
    }

    [Fact]
    public async Task Create_XuiServerWithoutAWebConfiguration_IsRejectedOnTheDirectTransportToo()
    {
        using var scope = App.Services.CreateScope();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel: false);
        config.WebApiConfig = null;

        (await Assert.ThrowsAsync<UserException>(() => application.Create(
                Model(config: config, features: ServerFeature.Xui | ServerFeature.V2ray))))
            .UserMessage.Should().Be("برای سرور x-ui پیکربندی وب لازم است!");
    }

    [Theory]
    [InlineData("host")]
    [InlineData("ssl")]
    [InlineData("port")]
    public async Task Create_XuiServerInTunnelMode_NeedsAPlainTextLoopbackWebConfiguration(string broken)
    {
        using var scope = App.Services.CreateScope();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel: true, port: 2053);

        switch (broken)
        {
            case "host": config.WebApiConfig!.HostName = "panel.local"; break;
            case "ssl": config.WebApiConfig!.Ssl = true; break;
            case "port": config.WebApiConfig!.Port = null; break;
        }

        (await Assert.ThrowsAsync<UserException>(() => application.Create(
                Model(config: config, features: ServerFeature.Xui | ServerFeature.V2ray))))
            .UserMessage.Should().Be("در حالت تانل، آدرس پنل x-ui باید `127.0.0.1` با پورت مشخص و بدون SSL باشد!");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_RouterInTunnelMode_NeedsALoopbackServiceAddress(bool with_web_config)
    {
        using var scope = App.Services.CreateScope();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel: true, port: 8728);

        // with the web block present but without a port, or without the block at all
        config.WebApiConfig!.Port = with_web_config ? null : 8728;

        if (!with_web_config) config.WebApiConfig = null;

        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(config: config))))
            .UserMessage.Should().Be("در حالت تانل، آدرس سرویس باید `127.0.0.1` با پورت مشخص باشد!");
    }

    [Theory]
    [InlineData(false, "username")]
    [InlineData(false, "password")]
    [InlineData(true, "username")]
    [InlineData(true, "password")]
    public async Task Create_XuiServerWithoutPanelCredentials_IsRejectedOnBothTransports(bool tunnel, string missing)
    {
        using var scope = App.Services.CreateScope();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel, port: 2053);

        if (missing == "username") config.WebApiConfig!.Username = " ";
        else config.WebApiConfig!.Password = null;

        (await Assert.ThrowsAsync<UserException>(() => application.Create(
                Model(config: config, features: ServerFeature.Xui | ServerFeature.V2ray))))
            .UserMessage.Should().Be("برای سرور x-ui اعتبار پنل لازم است!");
    }

    [Theory]
    [InlineData("username")]
    [InlineData("password")]
    [InlineData("config")]
    public async Task Create_UserManagerServerWithoutApiCredentials_IsRejected(string missing)
    {
        using var scope = App.Services.CreateScope();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel: false);

        switch (missing)
        {
            case "username": config.WebApiConfig!.Username = " "; break;
            case "password": config.WebApiConfig!.Password = null; break;
            case "config": config.WebApiConfig = null; break;
        }

        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(config: config))))
            .UserMessage.Should().Be("برای User Manager اعتبار API لازم است!");
    }

    [Theory]
    [InlineData("  SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=  ")]
    [InlineData("SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Create_HostKeyPinIsNormalizedBeforeItIsStored(string pasted)
    {
        using var scope = App.Services.CreateScope();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        var config = ChannelConfig(tunnel: true, port: 2053);

        config.SshConfig!.HostKeySha256 = pasted;
        config.SshConfig.PrivateKey = "  \n" + OpenSshKey + "\n  ";
        config.SshConfig.Username = " panel ";

        await application.Create(Model(name: "SRV-Tunnel1", domain: "tunnel1", ip: "10.90.0.2", config: config,
            features: ServerFeature.Xui | ServerFeature.V2ray));

        var created = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Tunnel1");

        created.Config!.SshConfig!.HostKeySha256.Should().Be("SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        created.Config.SshConfig.PrivateKey.Should().Be(OpenSshKey);
        created.Config.SshConfig.Username.Should().Be("panel");
        created.Config.Transport.Should().Be(ServerTransport.SshTunnel, "the round-trip through the stored config keeps the transport");

        // the stored JSON names the transport instead of a number: a row stays readable across a renumbering
        var plaintext = JsonSerializer.Serialize(config);

        plaintext.Should().Contain("\"Transport\":\"SshTunnel\"");
        JsonSerializer.Deserialize<ServerConfiguration>(plaintext)!.Transport.Should().Be(ServerTransport.SshTunnel);
        plaintext.Should().Contain("\"HostKeySha256\":\"SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"");
    }

    [Fact]
    public async Task Create_TunnelServerWithoutARadiusFeature_NeedsNoServiceAddress()
    {
        using var scope = App.Services.CreateScope();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        // a NAS reached over SSH for its scripts or OpenVPN files has no management service to forward to
        var config = ChannelConfig(tunnel: true, port: null);

        await application.Create(Model(name: "SRV-NasTunnel", domain: "nastunnel", ip: "10.90.0.4", config: config,
            features: ServerFeature.Nas | ServerFeature.Ovpn));

        var created = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-NasTunnel");

        Assert.Equal(ServerTransport.SshTunnel, created.Config!.Transport);
        Assert.Null(created.Config.WebApiConfig);
    }

    [Fact]
    public async Task Update_AppliesTheSameChannelRules()
    {
        using var scope = App.Services.CreateScope();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        await InjectAdminAsync(scope);
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        await application.Create(Model(name: "SRV-Rule1", domain: "rule1", ip: "10.90.0.3", config: ChannelConfig()));

        var id = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Rule1").Id;

        var config = ChannelConfig(tunnel: true, port: 2053);

        config.SshConfig!.HostKeySha256 = null;

        (await Assert.ThrowsAsync<UserException>(() => application.Update(Model(id: id, config: config))))
            .UserMessage.Should().Be("کلید میزبان (host key) ثبت نشده یا نامعتبر است!");
    }

    private static async Task InjectAdminAsync(IServiceScope scope)
    {
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        await Task.CompletedTask;
    }

    /// <summary>A direct server that passes every channel rule (Docs/25 §4.6).</summary>
    private static ServerConfiguration ChannelConfig(bool tunnel = false, int? port = 8728)
    {
        return new ServerConfiguration
        {
            Transport = tunnel ? ServerTransport.SshTunnel : ServerTransport.Direct,
            SshConfig = new SshConfig
            {
                Username = "panel",
                Password = tunnel ? "" : "ssh-secret",
                Port = 22,
                PrivateKey = tunnel ? OpenSshKey : null,
                PassPhrase = tunnel ? "key-secret" : null,
                HostKeySha256 = tunnel ? "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" : null,
            },
            WebApiConfig = port == null
                ? null
                : new WebApiConfig
                {
                    HostName = tunnel ? "127.0.0.1" : "192.168.125.50",
                    Port = port,
                    Username = "admin",
                    Password = "api-secret",
                },
        };
    }

    private const string OpenSshKey =
        "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtZW\nQ255MTkAAAAA\n-----END OPENSSH PRIVATE KEY-----";

    [Fact]
    public async Task Create_CapacityUpperBound_IsInclusive()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        // exactly 1 PiB is the last accepted value (the admin form sends GB × 1024³)
        await application.Create(Model(name: "SRV-Cap02", domain: "cap02", ip: "10.60.0.1", capacity_bytes: OnePebibyte));

        var created = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Cap02");

        Assert.Equal(OnePebibyte, created.CapacityBytes);
        Assert.Equal(OnePebibyte, (await application.GetList()).Data!.Single(row => row.Id == created.Id).CapacityBytes);
    }

    [Fact]
    public async Task Create_DuplicateIdentityFields_RejectedWith409()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        // against the seeded fixture rows (ip 192.168.125.11 / name Server1)
        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(ip: "192.168.125.11", name: "Fresh01", domain: "fresh01"))))
            .HttpCode.Should().Be(409);

        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(ip: "10.0.0.9", name: "Server1", domain: "fresh02"))))
            .HttpCode.Should().Be(409);

        // within the same batch: a domain claimed moments ago
        await application.Create(Model(name: "SRV-Dup01", domain: "dup01", ip: "10.77.0.1"));

        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(name: "SRV-Dup02", domain: "dup01", ip: "10.77.0.2"))))
            .HttpCode.Should().Be(409);
    }

    [Fact]
    public async Task Update_KeepAndReplaceConfig_WriteWithoutRead()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        var first = ChannelConfig();
        first.SshConfig!.Password = "first";

        await application.Create(Model(name: "SRV-Upd01", domain: "upd01", ip: "10.30.0.1", config: first));

        var id = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Upd01").Id;
        var stored_blob = server_moq.Data[id].JsonConfig;

        // no config sent → the stored protected blob is carried over untouched
        await application.Update(Model(id: id, name: "SRV-Upd01B", domain: "upd01", ip: "10.30.0.1"));

        Assert.Equal("SRV-Upd01B", server_moq.Data[id].Name);
        Assert.Equal(stored_blob, server_moq.Data[id].JsonConfig);

        // config sent → replaced wholesale
        var second = ChannelConfig();
        second.SshConfig!.Password = "second";

        await application.Update(Model(id: id, name: "SRV-Upd01B", domain: "upd01", ip: "10.30.0.1", config: second));

        Assert.NotEqual(stored_blob, server_moq.Data[id].JsonConfig);
        Assert.Equal("second", server_moq.Data[id].Config!.SshConfig!.Password);

        // unknown id / missing id
        await Assert.ThrowsAsync<UserException>(() => application.Update(Model(id: 999_999, name: "Ghost", domain: "ghost", ip: "10.30.0.9")));
        await Assert.ThrowsAsync<UserException>(() => application.Update(Model(name: "NoId", domain: "noid", ip: "10.30.0.8")));

        // a conflicting rename against another row is still a 409
        (await Assert.ThrowsAsync<UserException>(() => application.Update(Model(id: id, ip: "192.168.125.11", name: "SRV-Upd01B", domain: "upd01"))))
            .HttpCode.Should().Be(409);
    }

    [Fact]
    public async Task SetActive_TogglesLifecycle()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        await application.Create(Model(name: "SRV-Act01", domain: "act01", ip: "10.40.0.1"));

        var id = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Act01").Id;

        await application.SetActive(id, false);
        Assert.False(server_moq.Data[id].IsActive);

        await application.SetActive(id, true);
        Assert.True(server_moq.Data[id].IsActive);

        await Assert.ThrowsAsync<UserException>(() => application.SetActive(999_999, false));
        await Assert.ThrowsAsync<UserException>(() => application.SetActive(0, true));

        // non-admin is refused in depth
        job_context.InjectJobContext(4, "User4", "User4");

        (await Assert.ThrowsAsync<UserException>(() => application.SetActive(id, false)))
            .HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task Create_ConfigTooLongForColumn_Rejected()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        var oversized = ChannelConfig();

        oversized.WebApiConfig!.Password = new string('x', 6_000);

        (await Assert.ThrowsAsync<UserException>(() => application.Create(Model(name: "SRV-Big01", domain: "big01", config: oversized))))
            .UserMessage.Should().Contain("حجم کانفیگ"); // the channel rules passed; only the size failed
    }

    [Fact]
    public async Task CreateAndUpdate_MaxClientsRoundTrip()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        await application.Create(Model(name: "SRV-Cap01", domain: "cap01", ip: "10.50.0.1",
            features: ServerFeature.Xui | ServerFeature.V2ray, max_clients: 5));

        var id = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Cap01").Id;

        Assert.Equal(5, server_moq.Data[id].MaxClients);

        // a null on update clears the cap — same contract as the optional form field
        await application.Update(Model(id: id, name: "SRV-Cap01", domain: "cap01", ip: "10.50.0.1",
            features: ServerFeature.Xui | ServerFeature.V2ray));

        Assert.Null(server_moq.Data[id].MaxClients);
    }
}
