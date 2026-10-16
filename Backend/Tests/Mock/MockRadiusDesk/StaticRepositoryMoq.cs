using Moq;
using PhotonBypass.RadiusDesk.Interfaces;

namespace PhotonBypass.Test.Mock.MockRadiusDesk;

internal class StaticRepositoryMoq : Mock<IStaticRepository>, IUnitLevelService
{
    public StaticRepositoryMoq()
    {
        Setup(x => x.WebCloudId).Returns(Task.FromResult(0));

        Setup(x => x.DefaultProfile).Returns(Task.FromResult(new PhotonBypass.RadiusDesk.Entity.ProfileEntity()));
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<StaticRepositoryMoq>();
        services.AddSingleton(provider => provider.GetRequiredService<StaticRepositoryMoq>().Object);
    }
}