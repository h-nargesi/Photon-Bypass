using PhotonBypass;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;

var app = WebApplication.CreateBuilder(args)
    .AddLogService()
    .AddAppServices()
    .AddPortalServices()
    .Build();

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
