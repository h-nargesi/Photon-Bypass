using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Application.Management.Model;

namespace PhotonBypass.Test.Mock.MockOptions;

internal class ManagementOptionsMoq : Mock<IOptions<ManagementOptions>>, IUnitLevelService, IOptionsMoq
{
    public readonly ManagementOptions Options = new()
    {
        DefaultCertPath = "Data/config.ovpn",
        DefaultPrivateKeyOVpn = "DefaultPrivateKeyOVpn",
        PaymentCards =
        [
            new PaymentCard { BankName = "Test Bank", CardNumber = "0000-0000-0000-0000", HolderName = "Test Holder" },
        ],
        WalletDeactivationThreshold = null,
    };

    public ManagementOptionsMoq()
    {
        Setup(options => options.Value).Returns(Options);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton(new ManagementOptionsMoq().Object);
    }
}
