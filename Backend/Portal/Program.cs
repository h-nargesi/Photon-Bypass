using Microsoft.AspNetCore.HttpOverrides;
using PhotonBypass;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;

var app = WebApplication.CreateBuilder(args)
    .AddPleskPrivateSecrets()
    .AddLogService()
    .AddAppServices()
    .AddPortalServices()
    .Build();

// Behind the Plesk reverse proxy every request arrives from the proxy IP:
// without the forwarded headers the rate limiter partitions all users into a
// single bucket (self-DoS). The default known-proxies (loopback) matches the
// Plesk local nginx; a remote proxy must be added to KnownProxies.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<ExceptionHandlingMiddlewareInDevelopment>();
}
else
{
    app.UseMiddleware<ExceptionHandlingMiddleware>();
}

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();

public partial class PortalProgram { }
