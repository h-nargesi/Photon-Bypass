using FluentAssertions;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Xui.Application;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Live accounting tier (Docs/23 §4.4): the real <see cref="XuiTrafficSync"/>
/// against the real local database and the real Xui-Test panel — with a
/// freshly added client whose counters are still zero. The moving-counter
/// variants (delta / epoch / rotation) live in XuiPanelTrafficSyncDataPlaneTest.
/// </summary>
[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelTrafficSyncIntegrationTest : OutSourceLevelServiceInitializer
{
    [Fact]
    public async Task FirstPoll_AnchorsToLiveCounter_AndWritesZeroDeltaRow()
    {
        using var scope = App.Services.CreateScope();
        await XuiLiveHarness.Register(scope, this);

        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        var server = XuiPanelTestServer.Build();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("sy-first");

        await using var fixture = await XuiLiveAccount.CreateAsync(scope.ServiceProvider, email);
        await api.Value.AddClient(server, Client(email));

        try
        {
            await scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>().Run();

            var renewal = await ReloadRenewal(scope, fixture);

            renewal.LastSeenCounter.Should().Be(0);
            renewal.LastPollTime.Should().NotBeNull();
            renewal.TrafficUsed.Should().Be(0);

            // D7: a zero row proves the collector saw the account (liveness),
            // it is not "no data"
            var rows = await TodayRows(scope, fixture);

            rows.Should().ContainSingle();
            rows[0].TotalData.Should().Be(0);
            rows[0].NasId.Should().Be(fixture.Server.Id);
        }
        finally
        {
            await api.Value.RemoveClient(server, (await api.Value.GetClient(server, email))!.Uuid);
        }
    }

    [Fact]
    public async Task RepeatPoll_NoCounterMovement_NoDriftNoDuplicateRows()
    {
        using var scope = App.Services.CreateScope();
        await XuiLiveHarness.Register(scope, this);

        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        var server = XuiPanelTestServer.Build();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("sy-repeat");

        await using var fixture = await XuiLiveAccount.CreateAsync(scope.ServiceProvider, email);
        await api.Value.AddClient(server, Client(email));

        try
        {
            var sync = scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>();

            await sync.Run();
            await fixture.ReleaseSyncLockAsync();
            await sync.Run();

            var renewal = await ReloadRenewal(scope, fixture);

            renewal.TrafficUsed.Should().Be(0);
            renewal.LastActiveTime.Should().BeNull();

            (await TodayRows(scope, fixture)).Should().ContainSingle();
        }
        finally
        {
            await api.Value.RemoveClient(server, (await api.Value.GetClient(server, email))!.Uuid);
        }
    }

    [Fact]
    public async Task UnknownPanelClient_IsSkipped_NoRowsNoFailure()
    {
        using var scope = App.Services.CreateScope();
        await XuiLiveHarness.Register(scope, this);

        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        var server = XuiPanelTestServer.Build();
        await XuiPanelCarrier.AttachAsync(api, server);
        var known = XuiPanelTestServer.Email("sy-known");
        var unknown = XuiPanelTestServer.Email("sy-ghost");

        await using var fixture = await XuiLiveAccount.CreateAsync(scope.ServiceProvider, known);
        await api.Value.AddClient(server, Client(known));
        await api.Value.AddClient(server, Client(unknown));

        try
        {
            var sync = scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>();

            var run = () => sync.Run();

            await run.Should().NotThrowAsync();

            // the known account is accounted …
            (await TodayRows(scope, fixture)).Should().ContainSingle();

            // … and the ghost client produced no rows for anyone (it has no
            // account id to attach them to) while the run stayed healthy
            var renewal = await ReloadRenewal(scope, fixture);
            renewal.LastPollTime.Should().NotBeNull();
        }
        finally
        {
            await api.Value.RemoveClient(server, (await api.Value.GetClient(server, known))!.Uuid);
            await api.Value.RemoveClient(server, (await api.Value.GetClient(server, unknown))!.Uuid);
        }
    }

    private static XuiClientModel Client(string email)
    {
        return new XuiClientModel { Email = email, Uuid = Guid.NewGuid().ToString() };
    }

    private static async Task<Domain.Plan.Entity.RenewalEntity> ReloadRenewal(
        IServiceScope scope, XuiLiveAccount fixture)
    {
        var renewal_repo = scope.ServiceProvider.GetRequiredService<IRenewalRepository>();

        return (await renewal_repo.GetLatestByAccountIds([fixture.Account.Id]))[fixture.Account.Id];
    }

    private static async Task<List<Domain.Plan.Entity.TrafficDataEntity>> TodayRows(
        IServiceScope scope, XuiLiveAccount fixture)
    {
        var traffic_repo = scope.ServiceProvider.GetRequiredService<ITrafficDataRepository>();
        var session_id = XuiTrafficRow.SessionId(fixture.Account.Id, fixture.Renewal.Id, DateTime.Now);

        return await traffic_repo.Fetch(fixture.Server.Id, [session_id]);
    }
}
