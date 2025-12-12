using PhotonBypass.Domain.Plan;

namespace PhotonBypass.Test.Radius;

public class SessionRadiusTest : ServiceInitializer
{
    [Fact]
    public async Task GetTrafficData_DisableRealm()
    {
        using var scope = App.Services.CreateScope();
        var session_radius_sync_service = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var traffics = await session_radius_sync_service.GetTrafficData(DateTime.Today.AddDays(-30));
        
        Assert.NotNull(traffics);
        Assert.Equal(25, traffics.Count);
    }
    
    [Fact]
    public async Task GetTrafficData_TimeLimit()
    {
        using var scope = App.Services.CreateScope();
        var session_radius_sync_service = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var traffics = await session_radius_sync_service.GetTrafficData(DateTime.Today.AddDays(-2));
        
        Assert.NotNull(traffics);
        Assert.Equal(2, traffics.Count);
    }
}