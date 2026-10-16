using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Management.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockOutSource;
using static PhotonBypass.StaticValues;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.Application;

public partial class ServerManagementServiceTest : UnitLevelServiceInitializer
{
    private const double RateBytesPerMegabit = 125_000;

    private const double WindowSeconds = 30 * 24 * 60 * 60;

    [Fact]
    public async Task GetAvailableRealm_Check()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var realm = await manager.GetAvailableRealm();

        Assert.NotNull(realm);
        Assert.Equal(3, realm.Id);
    }

    [Fact]
    public async Task GetAvailableRealm_AllRealmsFull_ThrowsUserException()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetAllActiveNasInRealm(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, List<ServerEntity>>());

        await Assert.ThrowsAsync<UserException>(() => manager.GetAvailableRealm());
    }

    [Fact]
    public async Task GetAvailableRealm_ZeroEffectiveCapacity_RealmsSkipped()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetAllActiveNasInRealm(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, List<ServerEntity>>
            {
                // realm 3 (the default winner) closed by zero bandwidth, realm 1 stays open
                [3] = [NewNas(31, 3, band_width: 0)],
                [1] = [NewNas(11, 1, band_width: 110)],
            });

        var realm = await manager.GetAvailableRealm();

        Assert.NotNull(realm);
        Assert.Equal(1, realm.Id);
    }

    [Fact]
    public async Task GetDefaultCertificate_MultiRealm()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();

        var context = await manager.GetDefaultCertificate(null);
        var nas_domain_list = await server_repo.GetAllActiveNasDomainInRealm(null);
        var remotes = string.Join("\n", nas_domain_list.Select(domain => $"remote {domain}"));

        Assert.NotNull(context);

        var text_file = Encoding.UTF8.GetString(context.CertFile);

        Assert.Contains("setenv FRIENDLY_NAME \"All\"", text_file);
        Assert.Contains(remotes, text_file);
    }

    [Fact]
    public async Task GetDefaultCertificate_SingleRealm()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();
        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();

        var realm_name = await realm_repo.GetName(1);
        var context = await manager.GetDefaultCertificate(1);
        var nas_domain_list = await server_repo.GetAllActiveNasDomainInRealm(1);
        var remotes = string.Join("\n", nas_domain_list.Select(domain => $"remote {domain}"));

        Assert.NotNull(context);

        var text_file = Encoding.UTF8.GetString(context.CertFile);

        Assert.Contains($"setenv FRIENDLY_NAME \"{realm_name}\"", text_file);
        Assert.Contains(remotes, text_file);
    }

    [Fact]
    public async Task UpdateTrafficData()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var traffic_repo_mock = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();

        var saved = false;
        traffic_repo_mock.OnBachSave += records =>
        {
            saved = true;
            Assert.NotNull(records);
            // 6 sessions unknown to the database + 9 rows updated in place
            // (EndSession/Data stamps); the equal rows are skipped entirely.
            Assert.Equal(15, records.Count());
        };

        await manager.UpdateTrafficData();

        Assert.True(saved);
    }

    [Fact]
    public async Task UpdateTrafficData_SecondCycleWithSameSessions_DoesNotInsertDuplicates()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var traffic_repo_mock = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();

        var original_ids = traffic_repo_mock.Data.Select(traffic => traffic.Id).ToHashSet();

        var first_cycle = true;
        var second_cycle_save_count = 0;

        traffic_repo_mock.OnBachSave += records =>
        {
            if (!first_cycle) second_cycle_save_count += records.Count();
        };

        await manager.UpdateTrafficData();

        first_cycle = false;

        var row_count_after_first_cycle = traffic_repo_mock.Data.Count;

        await manager.UpdateTrafficData();

        // no new rows in the second cycle: every fetched session matched its row
        Assert.Equal(row_count_after_first_cycle, traffic_repo_mock.Data.Count);
        Assert.Equal(0, second_cycle_save_count);

        // existing rows were updated in place, not replaced by new inserts
        Assert.True(original_ids.IsSubsetOf(traffic_repo_mock.Data.Select(traffic => traffic.Id)));

        // (NasId, SessionId) is unique after two consecutive sync cycles
        Assert.All(
            traffic_repo_mock.Data.GroupBy(traffic => (traffic.NasId, traffic.SessionId)),
            group => Assert.Equal(1, group.Count()));
    }

    [Fact]
    public async Task CheckUserServerBalance_Check()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var social_moq = scope.ServiceProvider.GetRequiredService<SocialMediaServiceMoq>();

        IReadOnlyList<string>? reported_alarms = null;
        social_moq.OnAlarmServerCapacity += alarms => reported_alarms = alarms;

        await manager.CheckUserServerBalance();

        Assert.NotNull(reported_alarms);
        Assert.NotEmpty(reported_alarms);

        // observed usage in the mock data (505/110, 569/150, 694/100 bytes) is far above 90%
        Assert.All(reported_alarms, alarm => Assert.Contains("Low Capacity", alarm));

        var names = string.Join('|', reported_alarms);
        Assert.Contains("Realm-LO-1", names);
        Assert.Contains("Realm-LO-3", names);
        Assert.Contains("Realm-LO-4", names);

        Assert.All(reported_alarms, alarm =>
        {
            var match = CapacityAlarmPattern().Match(alarm);
            Assert.True(match.Success, alarm);
            Assert.True(double.TryParse(match.Groups[1].Value, out _), alarm);
            Assert.True(long.TryParse(match.Groups[2].Value, out _), alarm);
        });
    }

    [Fact]
    public async Task CheckUserServerBalance_ZeroEffectiveCapacity_RealmAlarmedNoMore()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var social_moq = scope.ServiceProvider.GetRequiredService<SocialMediaServiceMoq>();

        await manager.SetRealmCapacityOverride(4, 0);

        IReadOnlyList<string>? reported_alarms = null;
        social_moq.OnAlarmServerCapacity += alarms => reported_alarms = alarms;

        await manager.CheckUserServerBalance();

        Assert.NotNull(reported_alarms);
        Assert.All(reported_alarms, alarm => Assert.DoesNotContain("Realm-LO-4", alarm));
    }

    [Fact]
    public async Task GetRealmsCapacityState_ObservedUsage_IsThirtyDaySumNotSessionAverage()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.True(states.ContainsKey(1));
        // NasId=1 rows: (100+5) + (90+74) + (120+50) + (65+1) = 505 total in the window,
        // the old metric averaged per-session instead of summing the window
        Assert.Equal(505, states[1].ObservedUsageBytes);
        Assert.Equal(110, states[1].ComputedCapacityBytes);
        Assert.Equal(110, states[1].EffectiveCapacityBytes);
        Assert.Null(states[1].OverrideBytes);
    }

    [Fact]
    public async Task GetRealmsCapacityState_QuotaOnlyPlan_ScalesToThirtyDayWindow()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        UsePlanStates(scope, NewPlan(realm_id: 3, traffic_limit: 10 * BytesInGigLong, days: 30));

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(10 * BytesInGigLong, states[3].DemandBytes);
        Assert.Equal((10 * BytesInGigLong) / 150.0, states[3].Rate);
    }

    [Fact]
    public async Task GetRealmsCapacityState_RateOnlyPlan_UsesRateCeiling()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        UsePlanStates(scope, NewPlan(realm_id: 3, rate_limit_meg: 8, users: 2));

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(8 * RateBytesPerMegabit * WindowSeconds * 2, states[3].DemandBytes);
    }

    [Fact]
    public async Task GetRealmsCapacityState_QuotaAndRate_TakesTheMinimum()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        // quota bound = 100 GiB/30d, rate bound = 8 Mbps * 2 users is far larger: min = quota
        UsePlanStates(scope, NewPlan(realm_id: 3, traffic_limit: 100 * BytesInGigLong, days: 30, rate_limit_meg: 8, users: 2));

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(100 * BytesInGigLong, states[3].DemandBytes);
    }

    [Fact]
    public async Task GetRealmsCapacityState_UnlimitedPlan_FallsBackToConfiguredDemand()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        UsePlanStates(scope, NewPlan(realm_id: 3));

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(25 * BytesInGigLong, states[3].DemandBytes);
    }

    [Fact]
    public async Task GetRealmsCapacityState_QuotaWithoutWindow_FallsBackToConfiguredDemand()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        // traffic quota without a time window has an unknown monthly rate: use the fallback
        UsePlanStates(scope, NewPlan(realm_id: 3, traffic_limit: 10 * BytesInGigLong, days: null));

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(25 * BytesInGigLong, states[3].DemandBytes);
    }

    [Fact]
    public async Task GetRealmsCapacityState_UnrestrictedPlan_CountsInEveryRealm()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        UsePlanStates(scope, NewPlan(realm_id: null, traffic_limit: BytesInGigLong, days: 30));

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal([1, 3, 4], states.Keys.Order().ToArray());
        Assert.All(states.Values, state => Assert.Equal(BytesInGigLong, state.DemandBytes));
    }

    [Fact]
    public async Task GetRealmsCapacityState_NoDemand_RateIsZeroButEligible()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        UsePlanStates(scope);

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.All(states.Values, state =>
        {
            Assert.Equal(0, state.DemandBytes);
            Assert.Equal(0, state.Rate);
            Assert.True(state.IsEligibleForAssignment);
        });
    }

    [Fact]
    public async Task GetRealmsCapacityState_ExpiredAndExhaustedPlansExcluded()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        UsePlanStates(scope,
            NewPlan(realm_id: 3, traffic_limit: BytesInGigLong, days: 30, expired: true),
            NewPlan(realm_id: 3, traffic_limit: BytesInGigLong, days: 30, traffic_left: 0),
            NewPlan(realm_id: 3, traffic_limit: BytesInGigLong, days: 30, inactive: true),
            NewPlan(realm_id: 3, traffic_limit: BytesInGigLong, days: 30));

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(BytesInGigLong, states[3].DemandBytes);
    }

    [Fact]
    public async Task GetRealmsCapacityState_PlanOnInactiveRealm_DoesNotLeakIntoCapacityList()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        // realm 2 is inactive in the mock data: its plan demand must disappear
        UsePlanStates(scope, NewPlan(realm_id: 2, traffic_limit: BytesInGigLong, days: 30));

        var states = await manager.GetRealmsCapacityState();

        Assert.DoesNotContain(states, s => s.RealmId == 2);
        Assert.All(states, s => Assert.Equal(0, s.DemandBytes));
    }

    [Fact]
    public async Task SetRealmCapacityOverride_NegativeValue_ThrowsWithoutUpdate()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var realm_moq = scope.ServiceProvider.GetRequiredService<RealmRepositoryMoq>();

        var action = () => manager.SetRealmCapacityOverride(3, -1);

        await action.Should().ThrowAsync<UserException>();
        realm_moq.Verify(repo => repo.SetCapacityOverride(It.IsAny<int>(), It.IsAny<long?>()), Times.Never);
    }

    [Fact]
    public async Task SetRealmCapacityOverride_UnknownRealm_Throws()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var action = () => manager.SetRealmCapacityOverride(9900, 100);

        await action.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task SetRealmCapacityOverride_PositiveValue_ReplacesCapacityAbsolutely()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var realm_moq = scope.ServiceProvider.GetRequiredService<RealmRepositoryMoq>();

        UsePlanStates(scope, NewPlan(realm_id: 3, traffic_limit: BytesInGigLong, days: 30));

        var result = await manager.SetRealmCapacityOverride(3, 10 * BytesInGigLong);

        Assert.Equal(2, result.Code / 100);
        realm_moq.Verify(repo => repo.SetCapacityOverride(3, 10 * BytesInGigLong), Times.Once);

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(150, states[3].ComputedCapacityBytes);
        Assert.Equal(10 * BytesInGigLong, states[3].OverrideBytes);
        Assert.Equal(10 * BytesInGigLong, states[3].EffectiveCapacityBytes);
        Assert.Equal(BytesInGigLong / (10.0 * BytesInGigLong), states[3].Rate);
    }

    [Fact]
    public async Task SetRealmCapacityOverride_ZeroValue_ClosesRealm()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var result = await manager.SetRealmCapacityOverride(3, 0);

        Assert.Equal(2, result.Code / 100);

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Equal(0, states[3].EffectiveCapacityBytes);
        Assert.Equal(-1, states[3].Rate);
        Assert.False(states[3].IsEligibleForAssignment);

        // the closed realm loses the assignment to the next candidate (realm 1)
        var realm = await manager.GetAvailableRealm();
        Assert.Equal(1, realm.Id);
    }

    [Fact]
    public async Task SetRealmCapacityOverride_NullValue_RestoresComputedCapacity()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        await manager.SetRealmCapacityOverride(3, 10 * BytesInGigLong);
        await manager.SetRealmCapacityOverride(3, null);

        var states = (await manager.GetRealmsCapacityState()).ToDictionary(s => s.RealmId);

        Assert.Null(states[3].OverrideBytes);
        Assert.Equal(states[3].ComputedCapacityBytes, states[3].EffectiveCapacityBytes);
    }

    [Fact]
    public async Task GetDataPlaneInfo_RestrictedUserManagerRealm_WithOvpnNas()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var info = await manager.GetDataPlaneInfo(1);

        Assert.NotNull(info);
        Assert.Equal("usermanager", info.Kind);
        Assert.True(info.SupportsOvpnCert);
        Assert.True(info.SupportsPasswordChange);
    }

    [Fact]
    public async Task GetDataPlaneInfo_OvpnBitWithoutImplementedOsType_NoCertSupport()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetActiveNasInRealmOrAll(1))
            .ReturnsAsync([NewNas(11, 1, os_type: OperatingSystem.Ubuntu)]);

        var info = await manager.GetDataPlaneInfo(1);

        Assert.NotNull(info);
        Assert.False(info.SupportsOvpnCert);
    }

    [Fact]
    public async Task GetDataPlaneInfo_MixedRadiusKind_KindIsNull()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(1))
            .ReturnsAsync(
            [
                NewRadius(11, 1, ServerFeature.UserManager),
                NewRadius(12, 1, ServerFeature.Xui),
            ]);

        var info = await manager.GetDataPlaneInfo(1);

        Assert.NotNull(info);
        Assert.Null(info.Kind);
    }

    [Fact]
    public async Task GetDataPlaneInfo_XuiKind_DisablesPasswordChange()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(1))
            .ReturnsAsync([NewRadius(11, 1, ServerFeature.Xui)]);

        var info = await manager.GetDataPlaneInfo(1);

        Assert.NotNull(info);
        Assert.Equal("xui", info.Kind);
        Assert.False(info.SupportsPasswordChange);
    }

    [Fact]
    public async Task GetDataPlaneInfo_NoRadiusServer_DisablesPasswordChange()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        // realm 2 has only a NAS server (no radius bits) in the mock data
        var info = await manager.GetDataPlaneInfo(2);

        Assert.NotNull(info);
        Assert.Null(info.Kind);
        Assert.False(info.SupportsPasswordChange);
        Assert.True(info.SupportsOvpnCert);
    }

    [Fact]
    public async Task GetDataPlaneInfo_NullRealm_LegacyUserSeesEveryRealm()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var info = await manager.GetDataPlaneInfo(null);

        Assert.NotNull(info);
        Assert.Equal("usermanager", info.Kind);
        // any realm with a Mikrotik OVPN NAS keeps the cert path for legacy users
        Assert.True(info.SupportsOvpnCert);
        Assert.True(info.SupportsPasswordChange);
    }

    [Fact]
    public async Task GetDataPlaneInfo_NullRealm_AnyXuiRadius_DisablesPasswordChange()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetAllActiveRadius())
            .ReturnsAsync(
            [
                NewRadius(11, 1, ServerFeature.UserManager),
                NewRadius(50, 3, ServerFeature.Xui),
            ]);

        var info = await manager.GetDataPlaneInfo(null);

        Assert.NotNull(info);
        Assert.False(info.SupportsPasswordChange);
    }

    private static void UsePlanStates(IServiceScope scope, params PlanStateEntity[] plans)
    {
        scope.ServiceProvider.GetRequiredService<PlanStateRepositoryMoq>()
            .Setup(repo => repo.GetAll())
            .ReturnsAsync([.. plans]);
    }

    private static PlanStateEntity NewPlan(int? realm_id, long? traffic_limit = null, short? days = null,
        int? rate_limit_meg = null, byte users = 1, bool expired = false, long? traffic_left = null,
        bool inactive = false)
    {
        return new PlanStateEntity
        {
            Id = 1,
            Username = "User1",
            IsActive = !inactive,
            SimultaneousUser = users,
            RestrictedRealmId = realm_id,
            TrafficLimit = traffic_limit,
            TimeLimitInDays = days,
            RateLimitInMeg = rate_limit_meg,
            ExpirationDate = expired ? DateTime.Now.AddDays(-1) : DateTime.Now.AddDays(10),
            TrafficLeft = traffic_left,
        };
    }

    private static ServerEntity NewNas(int id, int realm_id, long band_width = 100,
        OperatingSystem os_type = OperatingSystem.Mikrotik)
    {
        return new ServerEntity
        {
            Id = id,
            IsActive = true,
            RealmId = realm_id,
            Name = $"Nas{id}",
            IpAddress = $"10.0.0.{id}",
            DomainName = $"nas{id}.photon-bypass.com",
            BandWidth = band_width,
            OsType = os_type,
            Features = ServerFeature.Ovpn,
        };
    }

    private static ServerEntity NewRadius(int id, int realm_id, ServerFeature radius_kind)
    {
        return new ServerEntity
        {
            Id = id,
            IsActive = true,
            RealmId = realm_id,
            Name = $"Radius{id}",
            IpAddress = $"10.1.0.{id}",
            DomainName = $"radius{id}.photon-bypass.com",
            BandWidth = 0,
            OsType = OperatingSystem.Mikrotik,
            Features = radius_kind,
        };
    }

    [GeneratedRegex(@"\((\d+(?:\.\d+)?)% from (\d+)\)")]
    private static partial Regex CapacityAlarmPattern();
}
