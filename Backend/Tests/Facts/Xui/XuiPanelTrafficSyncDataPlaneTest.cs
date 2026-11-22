using FluentAssertions;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Xui.Application;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Live accounting tier with moving counters (Docs/23 §4.4): the real
/// <see cref="XuiTrafficSync"/> against the real local database and real
/// VLESS traffic on the Xui-Test panel — the repeatable successor of the
/// one-time probe's headline findings (delta charging, epoch detection
/// after a manual reset, UUID rotation as a non-traffic event).
/// </summary>
[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelTrafficSyncDataPlaneTest : OutSourceLevelServiceInitializer
{
    private const int BigPaddingBytes = 300_000;

    private const int SmallPaddingBytes = 48_000;

    [Fact]
    public async Task Accounting_DeltaBetweenPolls_MatchesCounterMovement()
    {
        using var scope = App.Services.CreateScope();
        await XuiLiveHarness.Register(scope, this);

        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        var server = XuiPanelTestServer.Build();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("sd-delta");
        var uuid = Guid.NewGuid().ToString();

        await using var fixture = await XuiLiveAccount.CreateAsync(scope.ServiceProvider, email);
        await api.Value.AddClient(server, new XuiClientModel { Email = email, Uuid = uuid });

        try
        {
            var sync = scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>();

            await XuiPanelTrafficProbe.PushTrafficAsync(api, server, email, BigPaddingBytes);
            var first = await XuiPanelTrafficProbe.SettledTotalAsync(api, server, email, minimum: 100_000);

            await sync.Run();

            var after_first = await ReloadRenewal(scope, fixture);

            after_first.LastSeenCounter.Should().Be(first);
            after_first.TrafficUsed.Should().Be(first);
            after_first.LastActiveTime.Should().NotBeNull();
            (await TodayRows(scope, fixture)).Should().ContainSingle()
                .Which.TotalData.Should().Be(first);

            await XuiPanelTrafficProbe.PushTrafficAsync(api, server, email, SmallPaddingBytes);
            var second = await XuiPanelTrafficProbe.SettledTotalAsync(api, server, email, minimum: first + 10_000);

            second.Should().BeGreaterThan(first);

            await fixture.ReleaseSyncLockAsync();
            await sync.Run();

            var after_second = await ReloadRenewal(scope, fixture);

            after_second.LastSeenCounter.Should().Be(second);
            after_second.TrafficUsed.Should().Be(second);
            (await TodayRows(scope, fixture)).Should().ContainSingle()
                .Which.TotalData.Should().Be(second);
        }
        finally
        {
            await api.Value.RemoveClient(server, uuid);
        }
    }

    [Fact]
    public async Task Accounting_ResetAfterAnchor_RaisesEpochAlarmAndRebases()
    {
        using var scope = App.Services.CreateScope();
        await XuiLiveHarness.Register(scope, this);

        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        var server = XuiPanelTestServer.Build();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("sd-epoch");
        var uuid = Guid.NewGuid().ToString();

        await using var fixture = await XuiLiveAccount.CreateAsync(scope.ServiceProvider, email);
        await api.Value.AddClient(server, new XuiClientModel { Email = email, Uuid = uuid });

        try
        {
            var sync = scope.ServiceProvider.GetRequiredService<IXuiTrafficSync>();

            await XuiPanelTrafficProbe.PushTrafficAsync(api, server, email, BigPaddingBytes);
            var anchor = await XuiPanelTrafficProbe.SettledTotalAsync(api, server, email, minimum: 100_000);

            await sync.Run();

            // the reset-at-renewal operation (D12/D13) drops the live counter
            // below the anchor — the epoch path (D10) must charge the new
            // counter on top and rebase instead of fabricating a negative delta
            await api.Value.ResetClientTraffic(server, email);

            await XuiPanelTrafficProbe.PushTrafficAsync(api, server, email, 12_000);
            var rebased = await XuiPanelTrafficProbe.SettledTotalAsync(api, server, email, minimum: 4_000);

            rebased.Should().BeLessThan(anchor);

            await fixture.ReleaseSyncLockAsync();
            await sync.Run();

            var renewal = await ReloadRenewal(scope, fixture);

            renewal.LastSeenCounter.Should().Be(rebased);
            renewal.TrafficUsed.Should().Be(anchor + rebased);
            (await TodayRows(scope, fixture)).Should().ContainSingle()
                .Which.TotalData.Should().Be(anchor + rebased);
        }
        finally
        {
            await api.Value.RemoveClient(server, uuid);
        }
    }

    [Fact]
    public async Task UuidRotation_WithLiveCounters_IsNonTrafficEvent()
    {
        using var scope = App.Services.CreateScope();
        await XuiLiveHarness.Register(scope, this);

        var api = scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();
        var server = XuiPanelTestServer.Build();
        await XuiPanelCarrier.AttachAsync(api, server);
        var email = XuiPanelTestServer.Email("sd-rotate");
        var old_uuid = Guid.NewGuid().ToString();
        var new_uuid = Guid.NewGuid().ToString();

        await using var fixture = await XuiLiveAccount.CreateAsync(scope.ServiceProvider, email);
        await api.Value.AddClient(server, new XuiClientModel { Email = email, Uuid = old_uuid });

        try
        {
            await XuiPanelTrafficProbe.PushTrafficAsync(api, server, email, BigPaddingBytes);
            var before = await XuiPanelTrafficProbe.SettledTotalAsync(api, server, email, minimum: 100_000);
            var before_stats = await XuiPanelTrafficProbe.ReadStatsAsync(api, server, email);

            await api.Value.UpdateClient(server, old_uuid,
                new XuiClientModel { Email = email, Uuid = new_uuid, Enable = true });

            // the email-keyed stats row must survive the rotation untouched
            var after_stats = await XuiPanelTrafficProbe.ReadStatsAsync(api, server, email);

            after_stats.Up.Should().Be(before_stats.Up);
            after_stats.Down.Should().Be(before_stats.Down);
            after_stats.Total.Should().Be(before);

            // counting continues on the same row under the new uuid
            await XuiPanelTrafficProbe.PushTrafficAsync(api, server, email, SmallPaddingBytes);
            var grown = await XuiPanelTrafficProbe.SettledTotalAsync(api, server, email, minimum: before + 10_000);

            grown.Should().BeGreaterThan(before);

            var clients = await api.Value.ListClients(server);

            clients.Should().Contain(client => client.Uuid == new_uuid && client.Email == email);
            clients.Should().NotContain(client => client.Uuid == old_uuid);
        }
        finally
        {
            // the rotation may not have happened when the failure hit: the
            // client then still carries the old uuid and removing the new one
            // would fail (and leak it into the shared-collection state)
            try
            {
                await api.Value.RemoveClient(server, new_uuid);
            }
            catch
            {
                await api.Value.RemoveClient(server, old_uuid);
            }
        }
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
