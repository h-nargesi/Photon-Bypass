using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Application;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.Portal;

public static class ServiceFactory
{
    public static TBuilder AddPortalServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        LogConfiguration.InitializeLogService(builder.Configuration);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        //builder.Services.AddSwaggerGen();
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddMemoryCache();

        builder.Services.AddLazyScoped<IAccessService, AccessService>();
        builder.Services.AddLazyScoped<IJobContext, JobContext>();

        return builder;
    }
}
