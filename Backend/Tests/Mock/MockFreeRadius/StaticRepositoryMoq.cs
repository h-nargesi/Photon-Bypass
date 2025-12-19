using Moq;
using PhotonBypass.FreeRadius.Interfaces;

namespace PhotonBypass.Test.Mock.MockFreeRadius;

internal class StaticRepositoryMoq : Mock<IStaticRepository>, IOutSourceMoq
{
    public StaticRepositoryMoq()
    {
        Setup(x => x.WebCloudId);

        Setup(x => x.DefaultProfile);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<StaticRepositoryMoq>();
        services.AddSingleton(provider => provider.GetRequiredService<StaticRepositoryMoq>().Object);
    }
}