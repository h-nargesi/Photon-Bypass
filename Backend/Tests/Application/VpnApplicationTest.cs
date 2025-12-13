using PhotonBypass.Application.Vpn;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class VpnApplicationTest : ServiceInitializer
{
    private static readonly DateTime Today = DateTime.Now.Date;

    [Fact]
    public async Task TrafficData_User1()
    {
        using var scope = App.Services.CreateScope();

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .TrafficData("User1");

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(Today.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }

    [Fact]
    public async Task TrafficData_User2()
    {
        using var scope = App.Services.CreateScope();

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .TrafficData("User2");

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(Today.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }
}