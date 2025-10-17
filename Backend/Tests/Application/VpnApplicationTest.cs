using PhotonBypass.Application.Vpn;
using PhotonBypass.Test.MockOutSources;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class VpnApplicationTest : ServiceInitializer
{
    readonly static DateTime NOW = DateTime.Now.Date;

    public VpnApplicationTest()
    {
        var builder = Initialize();
        AddDefaultServices(builder);
        Build(builder);
    }

    [Fact]
    public async Task TrafficData_Overral()
    {
        var scope = CreateScope();

        var trafficRepo = scope.GetRequiredService<TrafficDataRepositoryMoq>();
        trafficRepo.OnBachSave += traffics =>
        {
            var ten = traffics.Where(x => x.Day == NOW.AddDays(-10)).FirstOrDefault();
            Assert.NotNull(ten);
            Assert.Equal(80, ten.DataIn);
            Assert.Equal(15, ten.DataOut);

            var eleven = traffics.Where(x => x.Day == NOW.AddDays(-11)).FirstOrDefault();
            Assert.Null(eleven);
        };

        var data = await CreateScope().GetRequiredService<IVpnApplication>()
            .TrafficData(string.Empty);

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(NOW.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }
}
