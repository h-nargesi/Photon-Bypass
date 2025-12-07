using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Connection;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class ConnectionApplicationTest : ServiceInitializer
{
    [Fact]
    public async Task GetCurrentConnectionState_CheckMerge()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var result_list = (await connection_app.GetCurrentConnectionState("User1")).Data;

        Assert.NotNull(result_list);
        Assert.Equal(4, result_list.Count);
    }
}