using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;

namespace PhotonBypass.Test.Facts.Portal;

public class ProductionSecretsGuardTest
{
    [Fact]
    public void Validate_ProductionWithAllCommittedTestValues_ThrowsNamingEveryKey()
    {
        var configuration = CommittedTestConfiguration();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Issuer:Code");
        exception.Message.Should().Contain("LocalDatabaseOptions:ConnectionString");
        exception.Message.Should().Contain("RadDapperOptions:ConnectionString");
        exception.Message.Should().Contain("RadiusServiceOptions:Password");
        exception.Message.Should().Contain("private/secrets.json");
    }

    [Fact]
    public void Validate_DevelopmentWithCommittedTestValues_Passes()
    {
        var configuration = CommittedTestConfiguration();
        var environment = HostEnvironment("Development");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ProductionWithRealSecrets_Passes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Issuer:Code"] = "cHJvZHVjdGlvbi1vbmx5LXNpZ25pbmcta2V5LW5vdC1hLXRlc3QtZml4dHVyZQ==",
            ["LocalDatabaseOptions:ConnectionString"] = "Server=prod-sql; Database=FastBypass; User Id=svc_bypass; Password=strong-secret; Encrypt=True;",
            ["RadDapperOptions:ConnectionString"] = "Server=prod-radius-db;Database=production_radius;User=radius;Password=strong-secret;",
            ["RadiusServiceOptions:Password"] = "a-real-radius-api-password",
        }).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ProductionWithOnlyTestJwtKey_ThrowsNamingOnlyThatKey()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Issuer:Code"] = CommittedTestSecretFixtures.JwtSigningKey,
            ["LocalDatabaseOptions:ConnectionString"] = "Server=prod-sql; Database=FastBypass; User Id=svc_bypass; Password=strong-secret; Encrypt=True;",
            ["RadDapperOptions:ConnectionString"] = "Server=prod-radius-db;Database=production_radius;User=radius;Password=strong-secret;",
            ["RadiusServiceOptions:Password"] = "a-real-radius-api-password",
        }).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Issuer:Code");
        exception.Message.Should().NotContain("LocalDatabaseOptions:ConnectionString");
        exception.Message.Should().NotContain("RadDapperOptions:ConnectionString");
        exception.Message.Should().NotContain("RadiusServiceOptions:Password");
    }

    [Fact]
    public void Validate_ProductionWithMissingKeys_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddPortalServices_ProductionWithCommittedTestValues_ThrowsFromGuard()
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production" });
        builder.Configuration.AddInMemoryCollection(CommittedTestValues());

        var act = () => builder.AddPortalServices();

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("ProductionSecretsGuard");
        exception.Message.Should().Contain("Issuer:Code");
    }

    [Fact]
    public void AddPortalServices_DevelopmentWithCommittedTestValues_CompletesRegistration()
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });
        builder.Configuration.AddInMemoryCollection(CommittedTestValues());

        var act = () => builder.AddPortalServices();

        act.Should().NotThrow();
    }

    private static IConfiguration CommittedTestConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(CommittedTestValues()).Build();

    private static IEnumerable<KeyValuePair<string, string?>> CommittedTestValues() =>
        new Dictionary<string, string?>
        {
            ["Issuer:Code"] = CommittedTestSecretFixtures.JwtSigningKey,
            ["LocalDatabaseOptions:ConnectionString"] = $"Server=.; Database={CommittedTestSecretFixtures.LocalDatabaseMarker}; User Id=test; Password=abc.123456; Encrypt=False;",
            ["RadDapperOptions:ConnectionString"] = $"Server=localhost;{CommittedTestSecretFixtures.RadDatabaseMarker}User=rd;Password=rd;",
            ["RadiusServiceOptions:Password"] = CommittedTestSecretFixtures.RadiusServicePassword,
        };

    private static IHostEnvironment HostEnvironment(string name) =>
        new StubHostEnvironment(name);

    private sealed class StubHostEnvironment(string environment_name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment_name;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
