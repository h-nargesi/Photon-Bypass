using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Domain.OutSource.Model;

namespace PhotonBypass.Test.Mock.MockOptions;

internal class BrandingOptionsMoq : Mock<IOptions<BrandingOptions>>, IUnitLevelService, IOptionsMoq
{
    public readonly BrandingOptions Options = new()
    {
        Title = "Fast Bypass",
        SiteUrl = "https://photon-bypass.com",
        ConnectionGuideUrl = "https://guide.example.com/vpn",
        OvpnInstallerUrl = "https://example.com/ovpn-installer",
        LogoPath = "Branding/logo.png",
    };

    public BrandingOptionsMoq()
    {
        Setup(options => options.Value).Returns(Options);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton(new BrandingOptionsMoq().Object);
    }
}
