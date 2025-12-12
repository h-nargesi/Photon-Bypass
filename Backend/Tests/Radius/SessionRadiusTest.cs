using PhotonBypass.Domain.Plan;

namespace PhotonBypass.Test.Radius;

public class SessionRadiusTest : ServiceInitializer
{
    [Fact]
    public async Task GetTrafficData()
    {
        using var scope = App.Services.CreateScope();
        var session_radius_sync_service = scope.ServiceProvider.GetRequiredService<ISessionRadiusSyncService>();
        var traffics = await session_radius_sync_service.GetTrafficData(DateTime.Today.AddDays(-10));
        
        Assert.NotNull(traffics);
        Assert.Equal(10, traffics.Count);
    }
}