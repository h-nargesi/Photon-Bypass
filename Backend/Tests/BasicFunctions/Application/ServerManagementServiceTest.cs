using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using PhotonBypass.Tools;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.BasicFunctions.Application;

public class ServerManagementServiceTest : ServiceInitializer
{
    public new static HostApplicationBuilder Initialize()
    {
        var builder = ServiceInitializer.Initialize();

        var realmRepo = new Mock<IRealmRepository>();
        realmRepo.Setup(x => x.FetchServerDensityEntity(It.IsAny<int>()))
            .Returns(Task.FromResult(ServerDensity));
        builder.Services.AddLazyScoped(s => realmRepo.Object);

        var cloud = new Mock<ICloudRepository>();
        cloud.Setup(x => x.FindWebCloud()).Returns(Task.FromResult(0));
        builder.Services.AddLazyScoped(s => cloud.Object);

        return builder;
    }

    [Fact]
    public async Task GetAvalableRealm_Check()
    {
        Build(Initialize());
        var manager = CreateScope().GetRequiredService<IServerManagementService>();

        var result = await manager.GetAvailableRealm(0);

        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
    }

    [Fact]
    public async Task CheckUserServerBalance_Check()
    {
        var builder = Initialize();

        var percent_Check = new Regex(@"\(([\d\.]+)%\s.*\s(\d+)\)");
        var isCalled = false;
        var social = new Mock<ISocialMediaService>();
        social.Setup(x => x.AlarmServerCapacity(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(alarms =>
            {
                isCalled = true;

                foreach (var alarm in alarms)
                {
                    var m = percent_Check.Match(alarm);

                    Assert.NotNull(m);

                    Assert.True(double.TryParse(m.Groups[1].Value, out var percent));
                    Assert.True(int.TryParse(m.Groups[2].Value, out var capacity));

                    if (capacity == 100)
                        Assert.Equal(8, percent);
                    else if (capacity == 300)
                        Assert.Equal(95, percent);
                    else 
                        Assert.True(false);
                }

                return Task.CompletedTask;
            });
        builder.Services.AddLazyScoped(s => social.Object);

        Build(builder);

        var manager = CreateScope().GetRequiredService<IServerManagementService>();

        await manager.CheckUserServerBalance();

        Assert.True(isCalled);
    }

    readonly static List<ServerDensityEntity> ServerDensity =
    [
        new ServerDensityEntity { Id = 1, Capacity = "100", UsersCount = 56 },
        new ServerDensityEntity { Id = 2, Capacity = "100", UsersCount = 55 },
        new ServerDensityEntity { Id = 3, Capacity = "100", UsersCount = 53 },
        new ServerDensityEntity { Id = 4, Capacity = "300", UsersCount = 285 },
        new ServerDensityEntity { Id = 5, Capacity = "300", UsersCount = 100 },
    ];
}
