using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Tools;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Application;

public partial class ServerManagementServiceTest : ServiceInitializer
{
    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        var social = new Mock<ISocialMediaService>();
        social.Setup(x => x.AlarmServerCapacity(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(alarms =>
            {
                OnSocialMediaCall?.Invoke(social, alarms);
                return Task.CompletedTask;
            });
        builder.Services.AddLazyScoped(_ => social.Object);
    }

    [Fact]
    public async Task GetAvailableRealm_Check()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var realm = await manager.GetAvailableRealm();

        Assert.NotNull(realm);
        Assert.Equal(4, realm.Id);
    }

    [Fact]
    public async Task CheckUserServerBalance_Check()
    {
        using var scope = App.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IServerManagementService>();

        var is_called = false;
        OnSocialMediaCall += (_, alarms) =>
        {
            is_called = true;

            foreach (var alarm in alarms)
            {
                var m = PercentCheck().Match(alarm);

                Assert.NotNull(m);

                Assert.True(double.TryParse(m.Groups[1].Value, out var percent));
                Assert.True(int.TryParse(m.Groups[2].Value, out var capacity));

                switch (capacity)
                {
                    case 100:
                        Assert.Equal(8, percent);
                        break;
                    case 300:
                        Assert.Equal(95, percent);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        };

        await manager.CheckUserServerBalance();

        Assert.True(is_called);
    }

    private event EventHandler<IEnumerable<string>>? OnSocialMediaCall;

    [GeneratedRegex(@"\(([\d\.]+)%\s.*\s(\d+)\)")]
    private static partial Regex PercentCheck();
}