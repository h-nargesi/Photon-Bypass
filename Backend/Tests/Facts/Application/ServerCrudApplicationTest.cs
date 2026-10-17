using FluentAssertions;
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
/// Admin server CRUD contract (docs/20 phase 5, D8): validation matrix
/// (ip/name/domain/bandwidth/os/features/realm), the 409 duplicate guard on
/// identity fields, write-without-read config handling and the admin-only
/// defense in depth. The list never carries the stored config — only scalars
/// and the has-config flag.
/// </summary>
public class ServerCrudApplicationTest : UnitLevelServiceInitializer
{
    private static SaveServerModel Model(
        int id = 0,
        string ip = "10.20.30.40",
        string name = "SRV-Unit01",
        string domain = "unit01",
        int realm_id = 5,
        long bandwidth = 1_073_741_824,
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
            BandWidth = bandwidth,
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
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
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
    }

    [Fact]
    public async Task Create_ValidationMatrix()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(ip: "not-an-ip")));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(ip: "2001:db8::1"))); // IPv6 exceeds varchar(15)
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(name: "")));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(name: new string('n', 16))));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(domain: new string('d', 16))));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(bandwidth: 0)));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(os: (OperatingSystem)99)));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(features: 0)));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(features: unchecked((ServerFeature)0x8000_0000))));
        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(realm_id: 999)));

        // happy path: created active with the config stored (protected or not,
        // the round-trip through the Config property must restore the payload)
        var config = new ServerConfiguration
        {
            SshConfig = new SshConfig { Username = "root", Password = "unit-secret", Port = 2222 },
        };

        await application.Create(Model(name: "SRV-Ok01", domain: "ok01", config: config));

        var created = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Ok01");

        Assert.True(created.IsActive);
        Assert.Equal(5, created.RealmId);
        Assert.NotNull(created.JsonConfig);
        Assert.Equal("unit-secret", created.Config!.SshConfig!.Password);
    }

    [Fact]
    public async Task Create_DuplicateIdentityFields_RejectedWith409()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
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
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        var first = new ServerConfiguration { SshConfig = new SshConfig { Username = "root", Password = "first" } };

        await application.Create(Model(name: "SRV-Upd01", domain: "upd01", ip: "10.30.0.1", config: first));

        var id = Assert.Single(server_moq.Data.Values, s => s.Name == "SRV-Upd01").Id;
        var stored_blob = server_moq.Data[id].JsonConfig;

        // no config sent → the stored protected blob is carried over untouched
        await application.Update(Model(id: id, name: "SRV-Upd01B", domain: "upd01", ip: "10.30.0.1"));

        Assert.Equal("SRV-Upd01B", server_moq.Data[id].Name);
        Assert.Equal(stored_blob, server_moq.Data[id].JsonConfig);

        // config sent → replaced wholesale
        var second = new ServerConfiguration { SshConfig = new SshConfig { Username = "root", Password = "second" } };

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
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
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
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IServerCrudApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        var oversized = new ServerConfiguration
        {
            DataBaseInfo = new DataBaseInfo { ConnectionString = new string('x', 6_000) },
        };

        await Assert.ThrowsAsync<UserException>(() => application.Create(Model(name: "SRV-Big01", domain: "big01", config: oversized)));
    }
}
