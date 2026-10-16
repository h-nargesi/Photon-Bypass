using Moq;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockOutSource;

public class SocialMediaServiceMoq : Mock<ISocialMediaService>, IUnitLevelService
{
    public event Action<IReadOnlyList<string>>? OnAlarmServerCapacity;

    public SocialMediaServiceMoq()
    {
        Setup(service => service.AlarmServerCapacity(It.IsAny<IEnumerable<string>>()))
            .Returns<IEnumerable<string>>(alarms =>
            {
                OnAlarmServerCapacity?.Invoke([.. alarms]);

                return Task.CompletedTask;
            });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<SocialMediaServiceMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<SocialMediaServiceMoq>().Object);
    }
}
