using Moq;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class VpnApplicationBasicFunctionTest : ServiceInitializer
{
    readonly static DateTime NOW = DateTime.Now.Date;

    [Fact]
    public async Task TrafficData_Overral()
    {
        var builder = Initialize();

        var trafficRepo = new Mock<ITrafficDataRepository>();
        trafficRepo.Setup(x => x.Fetch(It.IsNotNull<string>(), It.IsAny<DateTime>()))
            .Returns(Task.FromResult(TrafficData));
        trafficRepo.Setup(x => x.BachSave(It.IsNotNull<IEnumerable<TrafficDataEntity>>()))
            .Returns<IEnumerable<TrafficDataEntity>>(l =>
            {
                var ten = l.Where(x => x.Day == NOW.AddDays(-10)).FirstOrDefault();
                Assert.NotNull(ten);
                Assert.Equal(80, ten.DataIn);
                Assert.Equal(15, ten.DataOut);

                var eleven = l.Where(x => x.Day == NOW.AddDays(-11)).FirstOrDefault();
                Assert.Null(eleven);

                return Task.CompletedTask;
            });
        builder.Services.AddLazyScoped(s => trafficRepo.Object);

        var radiusRepo = new Mock<IRadiusService>();
        radiusRepo.Setup(x => x.FetchTrafficData(
            It.IsNotNull<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<TrafficDataRequestType>()))
            .Returns<string, DateTime, TrafficDataRequestType>((u, f, t) =>
            {
                Assert.Equal(NOW.AddDays(-9), f);
                Assert.Equal(TrafficDataRequestType.Monthly, t);
                return Task.FromResult(Radius);
            });
        builder.Services.AddLazyScoped(s => radiusRepo.Object);

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo.Setup(x => x.GetAccount(It.IsNotNull<string>()))
            .Returns(Task.FromResult((AccountEntity?)new AccountEntity()));
        builder.Services.AddLazyScoped(s => accountRepo.Object);

        Build(builder);

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

    readonly static IList<TrafficDataEntity> TrafficData =
    [
        new TrafficDataEntity { Day = NOW.AddDays(-10), DataIn = 100, DataOut = 5 },
        new TrafficDataEntity { Day = NOW.AddDays(-11), DataIn = 90, DataOut = 99 },
        new TrafficDataEntity { Day = NOW.AddDays(-12) },
        new TrafficDataEntity { Day = NOW.AddDays(-15) },
    ];

    readonly static TrafficDataRadius[] Radius =
    [
        new TrafficDataRadius { Day = NOW.AddDays(-10), DataIn = 80, DataOut = 15 },
        new TrafficDataRadius { Day = NOW.AddDays(-11), DataIn = 90, DataOut = 99 },
        new TrafficDataRadius { Day = NOW.AddDays(-9) },
        new TrafficDataRadius { Day = NOW.AddDays(-8) },
        new TrafficDataRadius { Day = NOW.AddDays(-6) },
        new TrafficDataRadius { Day = NOW.AddDays(-2) },
        new TrafficDataRadius { Day = NOW.AddDays(-1) },
    ];
}
