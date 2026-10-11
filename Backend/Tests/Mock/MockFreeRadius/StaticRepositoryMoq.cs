using Moq;
using PhotonBypass.FreeRadius.Interfaces;

namespace PhotonBypass.Test.Mock.MockFreeRadius;

internal class StaticRepositoryMoq : Mock<IStaticRepository>, IUnitLevelService
{
    public StaticRepositoryMoq()
    {
        Setup(x => x.WebCloudId).Returns(Task.FromResult(0));

        Setup(x => x.DefaultProfile).Returns(Task.FromResult(new PhotonBypass.FreeRadius.Entity.ProfileEntity()));
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<StaticRepositoryMoq>();
        services.AddSingleton(provider => provider.GetRequiredService<StaticRepositoryMoq>().Object);
    }
}