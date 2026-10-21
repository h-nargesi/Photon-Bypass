using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.OutSource;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Tools;
using System.Threading.RateLimiting;

namespace PhotonBypass.Portal;

public static class ServiceFactory
{
    public const string AuthRateLimitPolicy = "auth";

    /// <summary>Role-gated endpoints for managers and the admin (docs/20 D10).</summary>
    public const string ManagerPolicy = "Manager";

    /// <summary>Role-gated endpoints for the single admin account (docs/20 D9).</summary>
    public const string AdminPolicy = "Admin";

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

        ProductionSecretsGuard.Validate(builder.Configuration, builder.Environment);

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(ManagerPolicy, policy => policy.RequireRole(
                nameof(AccountRole.Manager), nameof(AccountRole.Admin)));
            options.AddPolicy(AdminPolicy, policy => policy.RequireRole(nameof(AccountRole.Admin)));
        });
        builder.Services.AddMemoryCache();

        builder.Services.AddLazyScoped<IAccessService, AccessService>();
        builder.Services.AddLazyScoped<IJobContext, JobContext>();

        // Telegram poller lives here (not in AddOutSourceServices) so the
        // Admin host — which clones the OutSource slice — never runs a second
        // concurrent getUpdates loop (Telegram answers 409 to one of them)
        builder.Services.AddHostedService<TelegramUpdatePoller>();

        return builder;
    }
}
