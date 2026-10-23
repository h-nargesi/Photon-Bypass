using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.OutSource;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Portal.Basical.Captcha;
using PhotonBypass.Tools;
using System.Threading.RateLimiting;

namespace PhotonBypass.Portal;

public static class ServiceFactory
{
    public const string AuthRateLimitPolicy = "auth";

    /// <summary>Anonymous forget-pass (reset-code issue) bucket.</summary>
    public const string ForgetRateLimitPolicy = "forget";

    /// <summary>Anonymous registration bucket.</summary>
    public const string RegisterRateLimitPolicy = "register";

    /// <summary>Anonymous reset-pass (reset-code consumption) bucket.</summary>
    public const string ResetRateLimitPolicy = "reset";

    /// <summary>Captcha image generation bucket (CPU guard).</summary>
    public const string CaptchaRateLimitPolicy = "captcha";

    /// <summary>
    /// Every endpoint rate-limit policy in one place: test hosts raise all of
    /// them to an unthrottled ceiling by iterating this list.
    /// </summary>
    public static readonly string[] AllRateLimitPolicies =
    [
        AuthRateLimitPolicy, ForgetRateLimitPolicy, RegisterRateLimitPolicy,
        ResetRateLimitPolicy, CaptchaRateLimitPolicy,
    ];

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
            AddFixedWindowPolicy(options, builder.Configuration, AuthRateLimitPolicy, 10, 60);
            AddFixedWindowPolicy(options, builder.Configuration, ForgetRateLimitPolicy, 3, 60);
            AddFixedWindowPolicy(options, builder.Configuration, RegisterRateLimitPolicy, 5, 60);
            AddFixedWindowPolicy(options, builder.Configuration, ResetRateLimitPolicy, 5, 60);
            AddFixedWindowPolicy(options, builder.Configuration, CaptchaRateLimitPolicy, 30, 60);
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
        builder.Services.AddLazyScoped<ICaptchaService, CaptchaService>();
        builder.Services.BindValidateReturn<CaptchaOptions>();

        // Telegram poller lives here (not in AddOutSourceServices) so the
        // Admin host — which clones the OutSource slice — never runs a second
        // concurrent getUpdates loop (Telegram answers 409 to one of them)
        builder.Services.AddHostedService<TelegramUpdatePoller>();

        return builder;
    }

    /// <summary>
    /// Per-endpoint fixed-window policy partitioned by client IP. Values are
    /// read as RateLimitOptions:{policy}:PermitLimit|WindowSeconds with a
    /// fallback to the former shared keys RateLimitOptions:PermitLimit|
    /// WindowSeconds — a host that raises the global keys (the scenario test
    /// factory) stays unthrottled on every policy at once.
    /// </summary>
    private static void AddFixedWindowPolicy(
        RateLimiterOptions options, IConfiguration configuration,
        string policy, int default_permit_limit, int default_window_seconds)
    {
        var permit_limit = configuration.GetValue(
            $"RateLimitOptions:{policy}:PermitLimit",
            configuration.GetValue("RateLimitOptions:PermitLimit", default_permit_limit));
        var window = TimeSpan.FromSeconds(configuration.GetValue(
            $"RateLimitOptions:{policy}:WindowSeconds",
            configuration.GetValue("RateLimitOptions:WindowSeconds", default_window_seconds)));

        options.AddPolicy(policy, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit_limit,
                    Window = window,
                    QueueLimit = 0,
                }));
    }
}
