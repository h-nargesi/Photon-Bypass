using PhotonBypass.Domain.Plan;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Radius;

public class SessionRadiusTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GetTrafficData_DisableRealm()
    {
        using var scope = App.Services.CreateScope();
        var traffic_data_repo = scope.ServiceProvider.GetRequiredService<ITrafficDataRepository>();
        var session_radius_sync_srv = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var last_update_times = await traffic_data_repo.LastUpdateTime();
        var traffics = await session_radius_sync_srv.GetTrafficData(last_update_times);

        Assert.NotNull(traffics);
        Assert.Equal(21, traffics.Count);
    }

    [Fact]
    public async Task GetTrafficData_TimeLimit()
    {
        using var scope = App.Services.CreateScope();
        var traffic_data_repo = scope.ServiceProvider.GetRequiredService<ITrafficDataRepository>();
        var session_radius_sync_srv = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var last_update_times = await traffic_data_repo.LastUpdateTime();
        var traffics = await session_radius_sync_srv.GetTrafficData(last_update_times);

        Assert.NotNull(traffics);
        Assert.Equal(21, traffics.Count);
    }
}