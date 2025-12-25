using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PhotonBypass.Result;
using PhotonBypass.Test.Initializer.OutSourceManager;
using static PhotonBypass.Test.Initializer.ProgramLevelInitializer;

namespace PhotonBypass.Test.Initializer;

public abstract class ProgramLevelInitializer(Factory factory) : IClassFixture<Factory>
{
    private const string TestPackageMikrotik = "Mikrotik-Scenario";
    private const string TestPackageDatabase = "DbScenario";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected readonly HttpClient Client = factory.CreateClient();
    protected IServiceProvider ServiceProvider => factory.Services;

    protected static Task<ApiResult<object>> CheckResponse(HttpResponseMessage response)
    {
        return CheckResponse<object>(response);
    }

    protected static Task<ApiResult<Dictionary<string, object>>> CheckResponseObject(HttpResponseMessage response)
    {
        return CheckResponse<Dictionary<string, object>>(response);
    }

    protected static Task<ApiResult<Dictionary<string, object>[]>> CheckResponseArray(HttpResponseMessage response)
    {
        return CheckResponse<Dictionary<string, object>[]>(response);
    }

    private static async Task<ApiResult<T>> CheckResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        var result = string.IsNullOrEmpty(content)
            ? null
            : JsonSerializer.Deserialize<ApiResult<T>>(content, Options);

        if (result != null)
        {
            return response.IsSuccessStatusCode && result.Code / 100 == 2
                ? result
                : throw new Exception(result.Developer ?? result.Message);
        }

        response.EnsureSuccessStatusCode();
        throw new Exception("Empty result!");
    }

    protected void SetToken(ApiResult<Dictionary<string, object>> token)
    {
        if (token.Data == null)
            throw new Exception($"Unexpected token: {token?.Data}");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.Data["access_token"].ToString());
    }

    public class Factory : WebApplicationFactory<PortalProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                ServiceInitializer.AddDefaultServices(services, [
                    typeof(IProgramLevelService),
                    typeof(IOutSourceLevelService),
                    typeof(IOptionsMoq)
                ]);

                var sp = services.BuildServiceProvider();

                using var scope = sp.CreateScope();

                var init_mikrotik =
                    Task.CompletedTask; //scope.Register<MikrotikInitializer>(TestPackageMikrotik, this);
                var init_database = scope.Register<LocalDatabaseInitializer>(TestPackageDatabase, this);

                Task.WaitAll(init_mikrotik, init_database);
            });
        }
    }
}