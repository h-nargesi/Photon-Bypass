using FluentAssertions;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class RealmCapacityOverrideTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "RealmCapacity";

    [Fact]
    public async Task SeededOverride_FeedsEffectiveCapacity()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var realms = await scope.ServiceProvider.GetRequiredService<IRealmRepository>()
            .FetchAllActiveRealm();

        realms.Select(realm => realm.Name).Should().BeEquivalentTo("Cap-Free", "Cap-Pinned", "Cap-Closed");

        var free = realms.Single(realm => realm.Name == "Cap-Free");
        var pinned = realms.Single(realm => realm.Name == "Cap-Pinned");
        var closed = realms.Single(realm => realm.Name == "Cap-Closed");

        var states = (await scope.ServiceProvider.GetRequiredService<IServerManagementService>()
                .GetRealmsCapacityState())
            .ToDictionary(state => state.RealmId);

        states[free.Id].ComputedCapacityBytes.Should().Be(10737418240);
        states[free.Id].OverrideBytes.Should().BeNull();
        states[free.Id].EffectiveCapacityBytes.Should().Be(10737418240);

        states[pinned.Id].OverrideBytes.Should().Be(5368709120);
        states[pinned.Id].EffectiveCapacityBytes.Should().Be(5368709120);

        states[closed.Id].EffectiveCapacityBytes.Should().Be(0);
        states[closed.Id].Rate.Should().Be(-1);
        states[closed.Id].IsEligibleForAssignment.Should().BeFalse();
    }

    [Fact]
    public async Task SetCapacityOverride_UpdatesAtomically()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var realms = await realm_repo.FetchAllActiveRealm();
        var free = realms.Single(realm => realm.Name == "Cap-Free");

        // missing realms report false instead of silently succeeding
        (await realm_repo.SetCapacityOverride(9900, null)).Should().BeFalse();

        (await realm_repo.SetCapacityOverride(free.Id, 1)).Should().BeTrue();

        var states = (await manager.GetRealmsCapacityState())
            .ToDictionary(state => state.RealmId);

        states[free.Id].OverrideBytes.Should().Be(1);
        states[free.Id].EffectiveCapacityBytes.Should().Be(1);

        // null restores the computed capacity
        (await realm_repo.SetCapacityOverride(free.Id, null)).Should().BeTrue();

        states = (await manager.GetRealmsCapacityState())
            .ToDictionary(state => state.RealmId);

        states[free.Id].OverrideBytes.Should().BeNull();
        states[free.Id].EffectiveCapacityBytes.Should().Be(10737418240);
    }
}
