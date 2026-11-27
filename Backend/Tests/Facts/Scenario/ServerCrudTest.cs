using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Test.Initializer;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace PhotonBypass.Test.Facts.Scenario;

/// <summary>
/// Admin server CRUD endpoints over real HTTP (Docs/20 phase 5, D8): the
/// list carries scalars + has-config only (neither plaintext nor protected
/// config ever leaves the server), create/update protect the sent config
/// JSON server-side (write-without-read), duplicates on identity fields are
/// 409, and the lifecycle toggle replaces the one-way deactivate.
/// </summary>
public class ServerCrudTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    private const string ScenarioIp = "192.168.56.12";

    private const string ScenarioName = "MK-Scenario";

    /// <summary>Camel-case save payload — <c>realm_id</c> null resolves the first active realm live.</summary>
    private async Task<Dictionary<string, string?>> SaveServer(
        bool create, string name, string domain, string ip, object? config = null,
        int? realm_id = null, long? features = null, int id = 0, int status = 2)
    {
        if (realm_id == null)
        {
            using var scope = ServiceProvider.CreateScope();
            var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();

            realm_id = (await realm_repo.FetchAllActiveRealm()).First().Id;
        }

        var response = await Client.PostAsJsonAsync(create ? "/api/server/create" : "/api/server/update", new
        {
            id,
            ipAddress = ip,
            name,
            domainName = domain,
            realmId = realm_id,
            capacityBytes = 10_737_418_240L,
            osType = 1,
            features = features ?? 0x802, // UserManager | V2ray
            config,
        });

        return await CheckResponseObject(response, status);
    }

    /// <summary>The web configuration a User Manager server needs (Docs/25 §4.6).</summary>
    private static object WebApi(string password) => new { hostName = ScenarioIp, port = 8728, username = "admin", password };

    private async Task<ServerEntity> FetchRow(string name)
    {
        using var scope = ServiceProvider.CreateScope();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        return (await server_repo.GetAllServers()).Single(s => s.Name == name);
    }

    [Fact]
    public async Task List_AdminOnly_ReturnsScalarsWithoutConfig()
    {
        // plain user and manager are refused by the Admin policy (Docs/20 §3)
        await Register("srv_user77", "77", "Passw0rd1");
        await Login("srv_user77", "Passw0rd1");
        await ServerList(status: 403);

        var manager = "srv_manager71";

        await InsertAccount(manager, UserTypes.AllowSelfService, AccountRole.Manager);
        await Login(manager, "Passw0rd1");
        await ServerList(status: 403);

        await Login("admin", "admin");

        var raw = await (await Client.GetAsync("/api/server/list")).Content.ReadAsStringAsync();

        // neither the config structure nor the protected blob ever leaks
        Assert.DoesNotContain("sshConfig", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("webApiConfig", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("databaseInfo", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protected:", raw);

        var rows = await ServerList();

        var seeded = Assert.Single(rows, r => r["name"] == ScenarioName);

        Assert.Equal(ScenarioIp, seeded["ipAddress"]);
        Assert.Equal("True", seeded["hasConfig"]); // the seeded row carries a plaintext config
        Assert.Equal("True", seeded["isActive"]);
        Assert.Equal("Abr01", seeded["realm"]);
        Assert.Equal("1026", seeded["features"]); // 0x402 — RadiusDesk | Ovpn
    }

    [Fact]
    public async Task Create_ProtectsConfigAndValidates()
    {
        await Login("admin", "admin");

        var secret = "S3cret-Scenario!";

        // a Radius server carries its web configuration too (Docs/25 §4.6)
        await SaveServer(create: true, name: "SRV-New72", domain: "new72", ip: "10.72.0.1",
            config: new { sshConfig = new { username = "root", password = secret, port = 22 }, webApiConfig = WebApi(secret) });

        // validation matrix → friendly 400s
        await SaveServer(create: true, name: "SRV-BadIp", domain: "badip", ip: "300.1.1.1", status: 4);
        await SaveServer(create: true, name: new string('n', 16), domain: "badname", ip: "10.72.0.2", status: 4);
        await SaveServer(create: true, name: "SRV-BadDom", domain: new string('d', 256), ip: "10.72.0.3", status: 4);
        await SaveServer(create: true, name: "SRV-BadRealm", domain: "badrealm", ip: "10.72.0.4",
            realm_id: 999_999, status: 4);
        await SaveServer(create: true, name: "SRV-NoFeat", domain: "nofeat", ip: "10.72.0.5", features: 0, status: 4);

        // duplicate identity against the seeded scenario row → 409
        await SaveServer(create: true, name: "SRV-Dup73", domain: "dup73", ip: ScenarioIp, status: 409);
        await SaveServer(create: true, name: ScenarioName, domain: "dup74", ip: "10.72.0.9", status: 409);

        var created = await FetchRow("SRV-New72");

        Assert.True(created.IsActive);
        Assert.True(ServerConfigProtection.IsProtected(created.JsonConfig!));
        Assert.Equal(secret, created.Config!.SshConfig!.Password);

        // the secret never appears in the list response
        var raw = await (await Client.GetAsync("/api/server/list")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(secret, raw);

        var rows = await ServerList();
        var row = Assert.Single(rows, r => r["name"] == "SRV-New72");

        Assert.Equal("True", row["hasConfig"]);
    }

    [Fact]
    public async Task Update_WriteWithoutRead_ReplacesOnDemand()
    {
        await Login("admin", "admin");

        await SaveServer(create: true, name: "SRV-Upd75", domain: "upd75", ip: "10.75.0.1",
            config: new { sshConfig = new { username = "root", password = "first-secret" }, webApiConfig = WebApi("first-secret") });

        var seeded = await FetchRow("SRV-Upd75");

        Assert.True(ServerConfigProtection.IsProtected(seeded.JsonConfig!));

        // scalars-only update: the stored protected blob is carried over untouched
        await SaveServer(create: false, name: "SRV-Upd75B", domain: "upd75", ip: "10.75.0.1",
            realm_id: seeded.RealmId, id: seeded.Id);

        var after_keep = await FetchRow("SRV-Upd75B");

        Assert.Equal(seeded.JsonConfig, after_keep.JsonConfig);

        // config update: replaced wholesale
        await SaveServer(create: false, name: "SRV-Upd75B", domain: "upd75", ip: "10.75.0.1",
            realm_id: seeded.RealmId, id: seeded.Id,
            config: new { sshConfig = new { username = "root", password = "second-secret" }, webApiConfig = WebApi("second-secret") });

        var after_replace = await FetchRow("SRV-Upd75B");

        Assert.NotEqual(seeded.JsonConfig, after_replace.JsonConfig);
        Assert.Equal("second-secret", after_replace.Config!.SshConfig!.Password);

        // unknown id / missing id → friendly 400s
        await SaveServer(create: false, name: "Ghost", domain: "ghost", ip: "10.75.0.9",
            realm_id: seeded.RealmId, id: 999_999, status: 4);

        await SaveServer(create: false, name: "NoId", domain: "noid", ip: "10.75.0.8",
            realm_id: seeded.RealmId, id: 0, status: 4);
    }

    [Fact]
    public async Task ConnectionTestAndHostChecks_AreAdminOnly()
    {
        // plain user and manager are refused by the Admin policy (Docs/20 §3)
        await Register("srv_user78", "78", "Passw0rd1");
        await Login("srv_user78", "Passw0rd1");
        await AssertServerToolsRefused();

        await InsertAccount("srv_manager72", UserTypes.AllowSelfService, AccountRole.Manager);
        await Login("srv_manager72", "Passw0rd1");
        await AssertServerToolsRefused();
    }

    [Fact]
    public async Task HostChecks_Admin_GetsTheThreeResults()
    {
        await Login("admin", "admin");

        var response = await Client.PostAsync("/api/server/host-checks", null);

        await CheckResponse(response);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var data = json.RootElement.GetProperty("data");
        var summary = data.GetProperty("summary").GetString()!;

        Assert.Contains("g3=observing", summary);
        Assert.Contains("g2=pass", summary); // the loopback address always accepts a listener
        Assert.True(data.GetProperty("loopbackBind").GetBoolean());

        var raw = data.GetRawText();

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("privateKey", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectionTest_UnreachableSshEndpoint_StopsAtTheTcpStep()
    {
        await Login("admin", "admin");

        using var reservation = new TcpListener(IPAddress.Loopback, 0);

        reservation.Start();

        var closed = ((IPEndPoint)reservation.LocalEndpoint).Port;

        reservation.Stop();

        // nothing is saved: the unsaved form is tested as sent
        var response = await Client.PostAsJsonAsync("/api/server/test-connection", new
        {
            id = 0,
            ipAddress = "127.0.0.1",
            name = "SRV-Probe",
            features = 0x800,
            config = new { sshConfig = new { username = "root", port = closed } },
        });

        await CheckResponse(response);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var data = json.RootElement.GetProperty("data");

        Assert.False(data.GetProperty("passed").GetBoolean());

        var step = Assert.Single(data.GetProperty("steps").EnumerateArray());

        Assert.Equal("tcp", step.GetProperty("step").GetString());
        Assert.Contains("ConnectionRefused", step.GetProperty("detail").GetString());

        var stored = await FetchAllNames();

        Assert.DoesNotContain("SRV-Probe", stored);
    }

    [Fact]
    public async Task Create_BindsTheTunnelTransportFromTheJsonBody()
    {
        await Login("admin", "admin");

        await SaveServer(create: true, name: "SRV-Tunnel79", domain: "tunnel79", ip: "10.79.0.1",
            config: TunnelConfig(OpenSshKey));

        var created = await FetchRow("SRV-Tunnel79");

        Assert.True(ServerConfigProtection.IsProtected(created.JsonConfig!));
        Assert.Equal(ServerTransport.SshTunnel, created.Config!.Transport);
        Assert.Equal("panel", created.Config.SshConfig!.Username);
        Assert.Equal(PinnedKey, created.Config.SshConfig.HostKeySha256);

        // the tunnel rules of Docs/25 §4.6 are enforced over HTTP as friendly 400s
        await SaveServer(create: true, name: "SRV-Tunnel80", domain: "tunnel80", ip: "10.79.0.2",
            config: TunnelConfig("-----BEGIN RSA PRIVATE KEY-----"), status: 4);
        await SaveServer(create: true, name: "SRV-Tunnel81", domain: "tunnel81", ip: "10.79.0.3",
            config: TunnelConfig(OpenSshKey, pin: null), status: 4);
    }

    private async Task AssertServerToolsRefused()
    {
        await CheckResponse(await Client.PostAsync("/api/server/host-checks", null), 403);
        await CheckResponse(await Client.PostAsJsonAsync("/api/server/test-connection", new { id = 1 }), 403);
    }

    private async Task<List<string>> FetchAllNames()
    {
        using var scope = ServiceProvider.CreateScope();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        return [.. (await server_repo.GetAllServers()).Select(server => server.Name)];
    }

    private const string OpenSshKey =
        "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtZW\nQ255MTkAAAAA\n-----END OPENSSH PRIVATE KEY-----";

    private const string PinnedKey = "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static object TunnelConfig(string key, string? pin = PinnedKey) => new
    {
        transport = "SshTunnel",
        sshConfig = new { username = "panel", port = 22, privateKey = key, hostKeySha256 = pin },
        webApiConfig = new { hostName = "127.0.0.1", port = 8728, username = "admin", password = "api-secret" },
    };

    [Fact]
    public async Task SetActive_TogglesLifecycle()
    {
        await Login("admin", "admin");

        await SaveServer(create: true, name: "SRV-Act76", domain: "act76", ip: "10.76.0.1");

        var id = (await FetchRow("SRV-Act76")).Id;

        await ServerSetActive(id, false);
        Assert.False((await FetchRow("SRV-Act76")).IsActive);

        await ServerSetActive(id, true);
        Assert.True((await FetchRow("SRV-Act76")).IsActive);

        await ServerSetActive(999_999, false, status: 4);
        await ServerSetActive(0, true, status: 4);
    }
}
