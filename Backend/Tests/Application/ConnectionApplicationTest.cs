using PhotonBypass.Application.Connection;

namespace PhotonBypass.Test.Application;

public class ConnectionApplicationTest : ServiceInitializer
{
    [Fact]
    public async Task GetCurrentConnectionState_CheckMerge()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var result_list = (await connection_app.GetCurrentConnectionState("User3")).Data;

        Assert.NotNull(result_list);
        Assert.Equal(2, result_list.Count);
    }
}