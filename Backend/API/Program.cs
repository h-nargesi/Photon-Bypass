using PhotonBypass.Infra;
using PhotonBypass.Application;
using Microsoft.IdentityModel.Tokens;
using PhotonBypass;

var builder = WebApplication.CreateBuilder(args);

LogConfiguration.InitializeLogService(
    builder.Environment.IsDevelopment(),
    builder.Configuration["Logging:FilePath"]?.ToString() ?? "event.log");

// Add services to the container.

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
builder.Services.AddDapperDbContext();
builder.Services.AddApplicationServices();
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
