using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Result;
using PhotonBypass.Test.Initializer.OutSourceManager;
using System.Text.Json;
using static PhotonBypass.Test.Initializer.ProgramLevelInitializer;

namespace PhotonBypass.Test.Initializer;

public abstract class ProgramLevelInitializer(Factory factory) : IClassFixture<Factory>
{
    private const string TestPackageMikrotik = "Mikrotik-Scenario";
    private const string TestPackageDatabase = "DbScenario";
    private readonly static JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected readonly HttpClient Client = factory.CreateClient();

    protected static async Task<ApiResult<Dictionary<string, object>>> CheckResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var result = string.IsNullOrEmpty(content) ? null : JsonSerializer.Deserialize<ApiResult<Dictionary<string, object>>>(content, options);

        if (!response.IsSuccessStatusCode && result != null)
            throw new Exception(result.Developer ?? result.Message);

        response.EnsureSuccessStatusCode();

        return result ?? throw new Exception("Empty result!");
    }

    protected void SetToken(ApiResult<Dictionary<string, object>> token)
    {
        if (token?.Data == null)
            throw new Exception($"Unexpected token: {token?.Data}");
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token?.Data["access_token"]}");
    }

    public class Factory : WebApplicationFactory<PortalProgram>
    {
        public IHost? App { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                ServiceInitializer.AddDefaultServices(services, [typeof(IOutSourceLevelService), typeof(IOptionsMoq)]);

                var sp = services.BuildServiceProvider();

                using var scope = sp.CreateScope();

                var init_mikrotik = Task.CompletedTask; //scope.Register<MikrotikInitializer>(TestPackageMikrotik, this);
                var init_database = scope.Register<LocalDatabaseInitializer>(TestPackageDatabase, this);

                Task.WaitAll(init_mikrotik, init_database);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            return App = base.CreateHost(builder);
        }
    }
}