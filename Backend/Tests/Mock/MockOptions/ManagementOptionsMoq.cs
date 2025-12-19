using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Application.Management;

namespace PhotonBypass.Test.Mock.MockOptions;

internal class ManagementOptionsMoq : Mock<IOptions<ManagementOptions>>, IOutSourceMoq, IOptionsMoq
{
    public ManagementOptionsMoq()
    {
        Setup(options => options.Value).Returns(new ManagementOptions
        {
            DefaultCertPath = "Data/config.ovpn",
            DefaultPrivateKeyOVpn =  "DefaultPrivateKeyOVpn",
        });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton(new ManagementOptionsMoq().Object);
    }
}