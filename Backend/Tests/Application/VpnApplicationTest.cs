using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Test.MockOutSources;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class VpnApplicationTest : ServiceInitializer
{
    private static readonly DateTime Now = DateTime.Now.Date;

    public VpnApplicationTest()
    {
        var builder = Initialize();
        AddDefaultServices(builder);
        Build(builder);
    }

    [Fact]
    public async Task TrafficData_Overall()
    {
        var services = CreateScope();

        var trafficRepo = services.GetRequiredService<TrafficDataRepositoryMoq>();
        trafficRepo.OnBachSave += traffics =>
        {
            var trafficDataEntities = traffics as TrafficDataEntity[] ?? traffics.ToArray();
            
            var ten = trafficDataEntities.FirstOrDefault(x => x.Day == Now.AddDays(-10));
            Assert.NotNull(ten);
            Assert.Equal(80, ten.DataIn);
            Assert.Equal(15, ten.DataOut);

            var eleven = trafficDataEntities.FirstOrDefault(x => x.Day == Now.AddDays(-11));
            Assert.Null(eleven);
        };

        var data = await CreateScope().GetRequiredService<IVpnApplication>()
            .TrafficData(string.Empty);

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(Now.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }
}
