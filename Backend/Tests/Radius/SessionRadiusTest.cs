using PhotonBypass.Domain.Plan;

namespace PhotonBypass.Test.Radius;

public class SessionRadiusTest : ServiceInitializer
{
    [Fact]
    public async Task GetTrafficData_DisableRealm()
    {
        using var scope = App.Services.CreateScope();
        var session_radius_sync_service = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var traffics = await session_radius_sync_service.GetTrafficData(DateTime.Today.AddDays(-10));
        
        Assert.NotNull(traffics);
        Assert.Equal(10, traffics.Count);
    }
    
    [Fact]
    public async Task GetTrafficData_TimeLimit()
    {
        using var scope = App.Services.CreateScope();
        var session_radius_sync_service = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var traffics = await session_radius_sync_service.GetTrafficData(DateTime.Today.AddDays(-1));
        
        Assert.NotNull(traffics);
        Assert.Equal(8, traffics.Count);
    }
}