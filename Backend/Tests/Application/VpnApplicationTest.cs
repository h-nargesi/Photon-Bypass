using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Test.MockOutSources;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class VpnApplicationTest : ServiceInitializer
{
    private static readonly DateTime Now = DateTime.Now.Date;

    [Fact]
    public async Task TrafficData_Overall()
    {
        using var scope = App.Services.CreateScope();
        
        var traffic_repo = scope.ServiceProvider.GetRequiredService<TrafficDataRepositoryMoq>();
        traffic_repo.OnBachSave += traffics =>
        {
            var traffic_data_entities = traffics as TrafficDataEntity[] ?? [.. traffics];

            var ten = traffic_data_entities.FirstOrDefault(x => x.Day == Now.AddDays(-10));
            Assert.NotNull(ten);
            Assert.Equal(80, ten.DataIn);
            Assert.Equal(15, ten.DataOut);

            var eleven = traffic_data_entities.FirstOrDefault(x => x.Day == Now.AddDays(-11));
            Assert.Null(eleven);
        };

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
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