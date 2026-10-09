using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Tools;
using System.Threading.RateLimiting;

namespace PhotonBypass.Portal;

public static class ServiceFactory
{
    public const string AuthRateLimitPolicy = "auth";

    public static TBuilder AddPortalServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            var permit_limit = builder.Configuration.GetValue("RateLimitOptions:PermitLimit", 10);
            var window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimitOptions:WindowSeconds", 60));
            options.AddPolicy(AuthRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permit_limit,
                        Window = window,
                        QueueLimit = 0,
                    }));
        });
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var issuer_signing_key = builder.Configuration["Issuer:Code"]
                    ?? throw new Exception("Issuer:Code is not set.");
                IdentityHelper.Key = new SymmetricSecurityKey(Convert.FromBase64String(issuer_signing_key));
                IdentityHelper.Issuer = builder.Configuration["Issuer:Name"];

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = IdentityHelper.Issuer,
                    ValidAudience = IdentityHelper.Issuer,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = IdentityHelper.Key,
                    ClockSkew = TimeSpan.Zero,
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddMemoryCache();

        builder.Services.AddLazyScoped<IAccessService, AccessService>();
        builder.Services.AddLazyScoped<IJobContext, JobContext>();

        return builder;
    }
}
