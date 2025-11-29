using PhotonBypass;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;

var app = WebApplication.CreateBuilder(args)
    .AddAppServices()
    .AddPortalServices()
    .Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

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

app.MapControllers();

app.Run();
