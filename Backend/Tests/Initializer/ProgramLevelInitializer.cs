using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
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

    protected static Task<ApiResult<object>> CheckResponse(HttpResponseMessage response, int state = 2)
    {
        return CheckResponse<object>(response, state);
    }

    protected static async Task<Dictionary<string, string>> CheckResponseObject(HttpResponseMessage response, int state = 2)
    {
        var result = await CheckResponse<Dictionary<string, object>>(response, state);
        return result.Data?.ToDictionary(k => k.Key, v => v.ToString()) ?? [];
    }

    protected static async Task<Dictionary<string, string>[]> CheckResponseArray(HttpResponseMessage response, int state = 2)
    {
        var result = await CheckResponse<Dictionary<string, object>[]>(response, state);
        return result.Data?.Select(record => record.ToDictionary(k => k.Key, v => v.ToString())).ToArray() ?? [];
    }

    private static async Task<ApiResult<T>> CheckResponse<T>(HttpResponseMessage response, int state)
    {
        var content = await response.Content.ReadAsStringAsync();

        var result = string.IsNullOrEmpty(content)
            ? null
            : JsonSerializer.Deserialize<ApiResult<T>>(content, Options);

        var code = result?.Code ?? (int)response.StatusCode;
        var exp = state < 10 ? code / 100
            : state < 100 ? code / 10
            : code;

        if (state != exp)
        {
            var message = result?.Developer ?? result?.Message ?? response.RequestMessage?.ToString();
            throw new Exception($"Code: {code}\n" + message);
        }

        Assert.NotNull(result);

        return result;
    }

    protected void SetToken(Dictionary<string, string> token)
    {
        if (token == null)
            throw new Exception($"Unexpected token: {token}");
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token["access_token"].ToString());
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

                var init_mikrotik = scope.Register<MikrotikInitializer>(TestPackageMikrotik, this);
                var init_database = scope.Register<LocalDatabaseInitializer>(TestPackageDatabase, this);

                Task.WaitAll(init_mikrotik, init_database);
            });
        }
    }
}