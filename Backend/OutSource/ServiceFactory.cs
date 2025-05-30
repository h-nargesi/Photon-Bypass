using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.OutSource;

public static class ServiceFactory
{
    public static void AddOutSourceServices<Builder>(this Builder builder) where Builder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<EmailOptions>(builder.Configuration);

        builder.Services.AddLazySingleton<IEmailService, EmailService>();
        builder.Services.AddLazySingleton<IVpnNodeService, VpnNodeService>();
        builder.Services.AddLazySingleton<ISocialMediaService, SocialMediaService>();
    }
}
