using Microsoft.IdentityModel.Tokens;
using PhotonBypass.API.Basical;
using PhotonBypass.Application;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.API;

public static class ServiceFactory
{
    public static TBuilder AddApiServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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
        builder.AddApplicationServices();

        return builder;
    }
}
