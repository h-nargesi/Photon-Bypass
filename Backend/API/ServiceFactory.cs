using Microsoft.IdentityModel.Tokens;
using PhotonBypass.API.Basical;
using PhotonBypass.Application;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;

namespace PhotonBypass.API;

public static class ServiceFactory
{
    public static Builder AddAppServices<Builder>(this Builder builder) where Builder : IHostApplicationBuilder
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

        builder.Services.AddScoped<IAccessService, AccessService>();
        builder.Services.AddScoped<IJobContext, JobContext>();
        builder.Services.AddApplicationServices();

        return builder;
    }
}
