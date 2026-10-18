using FluentAssertions;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.LocalDatabase;

/// <summary>
/// Real-SQL regression tests for the raw-SQL lookups of <c>ServerRepository</c>
/// that used to be covered by mocks only (docs/14 H21–H23): three live bugs —
/// the realm filter concatenated without a separator, the bare-list Dapper
/// parameter and the stray quote in the close-connection lookup — escaped the
/// mocked unit tests and only exploded on a real database.
/// </summary>
public class ServerRepositoryTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "ServerLookup";

    [Fact]
    public async Task GetAllActiveNasDomainInRealm_ScopedToRealm()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        var realms = await realm_repo.FetchAllActiveRealm();
        var realm_a = realms.Single(realm => realm.Name == "Lookup-A");
        var realm_b = realms.Single(realm => realm.Name == "Lookup-B");

        // used to die with a syntax error ("... > 0and RealmId = ...")
        (await server_repo.GetAllActiveNasDomainInRealm(realm_a.Id))
            .Should().BeEquivalentTo("nas-a1");

        (await server_repo.GetAllActiveNasDomainInRealm(realm_b.Id))
            .Should().BeEquivalentTo("nas-b1");
    }

    [Fact]
    public async Task GetAllActiveNasDomainInRealm_AllRealms()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        // active NAS servers across all realms (including the realm-less one);
        // the inactive NAS-A2 and the radius-only RAD-B1 stay out
        (await server_repo.GetAllActiveNasDomainInRealm(null))
            .Should().BeEquivalentTo("nas-a1", "nas-b1", "nas-wild");
    }

    [Fact]
    public async Task GetServerIdByIpAddress_BindsTheListParameter()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        var realms = await realm_repo.FetchAllActiveRealm();
        var realm_a = realms.Single(realm => realm.Name == "Lookup-A");
        var realm_b = realms.Single(realm => realm.Name == "Lookup-B");

        // used to die with "Must declare the scalar variable @ips": the list
        // was passed as the Dapper parameter template instead of a member
        var result = await server_repo.GetServerIdByIpAddress(
            ["192.168.58.21", "192.168.58.23", "192.168.58.99"]);

        result.Keys.Should().BeEquivalentTo("192.168.58.21", "192.168.58.23");

        var nas_a1 = (await server_repo.GetActiveNasInfo("192.168.58.21"))!;
        var nas_b1 = (await server_repo.GetActiveNasInfo("192.168.58.23"))!;

        result["192.168.58.21"].Should().Be((nas_a1.Id, realm_a.Id));

        result["192.168.58.23"].Should().Be((nas_b1.Id, realm_b.Id));
    }

    [Fact]
    public async Task GetActiveNasInfo_FindsActiveNasByIp()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        // used to die with "Unclosed quotation mark": a stray `")` sat inside
        // the where raw-string — this is the close-connection lookup
        var nas = await server_repo.GetActiveNasInfo("192.168.58.21");

        nas.Should().NotBeNull();
        nas!.Name.Should().Be("NAS-A1");
        nas.DomainName.Should().Be("nas-a1");

        // inactive NAS and radius-only servers never match
        (await server_repo.GetActiveNasInfo("192.168.58.22")).Should().BeNull();
        (await server_repo.GetActiveNasInfo("192.168.58.24")).Should().BeNull();
        (await server_repo.GetActiveNasInfo("192.168.58.99")).Should().BeNull();
    }
}
