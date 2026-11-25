using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Initializer;

public class TestAppSettingsHostTest : IDisposable
{
    private readonly ProbeFactory portal = new();

    [Fact]
    public void ScenarioHost_TestProjectFileWinsOverPortalDevelopmentSettings()
    {
        var actual = portal.Services.GetRequiredService<IConfiguration>();

        AssertFileWins(actual, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["RateLimitOptions:PermitLimit"] = "100000",
        });
        AssertConnectionStringWins(actual);
        actual["LocalDatabaseOptions:PackageDatabase"].Should().Be(portal.PackageDatabase);
        portal.PackageDatabase.Should().MatchRegex("^FastBypass_DbS[0-9a-f]{32}$");
        var issuer = actual["Issuer:Code"];
        issuer.Should().NotBeNullOrEmpty();
        var decode = () => Convert.FromBase64String(issuer!);
        decode.Should().NotThrow();
        actual["CaptchaOptions:Enabled"].Should().Be("false");
        actual["CaptchaOptions:CodeLength"].Should().Be("5");
        actual["ManagementOptions:AllowRegistration"].Should().Be("true");
    }

    [Fact]
    public void OutSourceBuilder_TestProjectFileWinsOverTheCopiedDevelopmentFile()
    {
        var previous = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = "Development",
            });
            TestAppSettings.Apply(builder.Configuration);

            AssertFileWins(builder.Configuration, new Dictionary<string, string?>());
            AssertConnectionStringWins(builder.Configuration);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previous);
        }
    }

    public void Dispose() => portal.Dispose();

    private static void AssertFileWins(IConfiguration actual, IReadOnlyDictionary<string, string?> harness)
    {
        foreach (var pair in TestAppSettings.Read())
        {
            var expected = harness.TryGetValue(pair.Key, out var replaced) ? replaced : pair.Value;
            actual[pair.Key].Should().Be(expected, because: pair.Key);
        }
    }

    /// <summary>
    /// The effective connection string is the test file's — the scenario
    /// database with whatever login that file carries — never Portal's
    /// development one. The expectation is read from the file, not spelled
    /// out, so changing the login does not break this test.
    /// </summary>
    private static void AssertConnectionStringWins(IConfiguration actual)
    {
        actual["LocalDatabaseOptions:ConnectionString"].Should().Be(
            TestAppSettings.Read()["LocalDatabaseOptions:ConnectionString"]);
    }

    /// <summary>
    /// Same configuration hook as the scenario factory, without booting VMs
    /// or the Quartz job that would otherwise fire on host start.
    /// </summary>
    private sealed class ProbeFactory : ProgramLevelInitializer.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ApplyTestConfiguration(builder);
            builder.ConfigureTestServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                {
                    services.Remove(descriptor);
                }
            });
        }
    }
}
