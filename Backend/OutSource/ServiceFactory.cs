using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Tools;

namespace PhotonBypass.OutSource;

public static class ServiceFactory
{
    public static void AddOutSourceServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<EmailOptions>();

        builder.Services.AddLazyScoped<IEmailService, EmailService>();
        builder.Services.AddLazyScoped<ISocialMediaService, SocialMediaService>();
    }
}
