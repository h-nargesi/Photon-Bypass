using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;

namespace PhotonBypass.Test.Facts.Portal;

public class RequiredSettingsGuardTest : IDisposable
{
    private readonly List<string> temp_roots = [];
    [Fact]
    public void Validate_RequiredKeysMissing_ThrowsNamingEveryKey()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var environment = HostEnvironment("Development");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Issuer:Code");
        exception.Message.Should().Contain("LocalDatabaseOptions:ConnectionString");
        exception.Message.Should().Contain("appsettings.Development.json");
    }

    [Fact]
    public void Validate_AllRequiredKeysPresent_Passes()
    {
        var configuration = BuildConfiguration(DevelopmentValues());
        var environment = HostEnvironment("Development");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_AllRequiredKeysPresentInProductionWithRealSecrets_Passes()
    {
        var configuration = BuildConfiguration(ProductionValues());
        var environment = HostEnvironment("Production");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_OnlyOneRequiredKeyMissing_ThrowsNamingOnlyThatKey()
    {
        var values = DevelopmentValues();
        values.Remove("Issuer:Code");

        var configuration = BuildConfiguration(values);
        var environment = HostEnvironment("Development");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Issuer:Code");
        exception.Message.Should().NotContain("LocalDatabaseOptions:ConnectionString");
    }

    [Fact]
    public void Validate_EmailAbsentInDevelopment_Passes()
    {
        // email is Development's logged no-op (Docs/14 M48): only presence
        // of the two all-environment keys is checked
        var values = DevelopmentValues();

        var configuration = BuildConfiguration(values);
        var environment = HostEnvironment("Development");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ProductionWithoutEmailOptions_ThrowsNamingTheEmailKeys()
    {
        var values = ProductionValues();
        values.Remove("EmailOptions:Enabled");
        values.Remove("EmailOptions:Address");
        values.Remove("EmailOptions:Password");

        var configuration = BuildConfiguration(values);
        var environment = HostEnvironment("Production");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("EmailOptions:Enabled");
        exception.Message.Should().Contain("EmailOptions:Address");
        exception.Message.Should().Contain("EmailOptions:Password");
        exception.Message.Should().NotContain("Issuer:Code");
        exception.Message.Should().Contain("private/secrets.json");
    }

    [Fact]
    public void Validate_ProductionWithDisabledEmail_ThrowsNamingEnabledOnly()
    {
        var values = ProductionValues();
        values["EmailOptions:Enabled"] = "false";

        var configuration = BuildConfiguration(values);
        var environment = HostEnvironment("Production");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("EmailOptions:Enabled");
        exception.Message.Should().NotContain("EmailOptions:Address");
        exception.Message.Should().NotContain("EmailOptions:Password");
    }

    [Fact]
    public void Validate_ProductionWithEmailWithoutCredentials_ThrowsNamingAddressAndPassword()
    {
        var values = ProductionValues();
        values["EmailOptions:Address"] = " ";
        values.Remove("EmailOptions:Password");

        var configuration = BuildConfiguration(values);
        var environment = HostEnvironment("Production");

        var act = () => RequiredSettingsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("EmailOptions:Address");
        exception.Message.Should().Contain("EmailOptions:Password");
        exception.Message.Should().NotContain("EmailOptions:Enabled");
    }

    [Fact]
    public void AddPortalServices_ProductionWithEmptyConfiguration_ThrowsFromGuard()
    {
        // empty content root: no appsettings bleed-in from the test output
        // directory — the guard sees a genuinely empty configuration
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Production",
            ContentRootPath = CreateEmptyRoot(),
        });
        builder.Configuration.AddInMemoryCollection([]);

        var act = () => builder.AddPortalServices();

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("RequiredSettingsGuard");
        exception.Message.Should().Contain("Issuer:Code");
        exception.Message.Should().Contain("LocalDatabaseOptions:ConnectionString");
    }

    [Fact]
    public void AddPortalServices_DevelopmentWithRequiredKeys_CompletesRegistration()
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
            ContentRootPath = CreateEmptyRoot(),
        });
        builder.Configuration.AddInMemoryCollection(DevelopmentValues());

        var act = () => builder.AddPortalServices();

        act.Should().NotThrow();
    }

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> DevelopmentValues() =>
        new()
        {
            ["Issuer:Code"] = "ZGV2ZWxvcG1lbnQtb25seS1zaWduaW5nLWtleS1maXh0dXJlLXZhbHVlLWhlcmU=",
            ["LocalDatabaseOptions:ConnectionString"] = "Server=.; Database=FastBypass_DbScenario; User Id=test; Password=abc.123456; Encrypt=False; TrustServerCertificate=True;",
        };

    private static Dictionary<string, string?> ProductionValues() =>
        new()
        {
            ["Issuer:Code"] = "cHJvZHVjdGlvbi1vbmx5LXNpZ25pbmcta2V5LW5vdC1hLXRlc3QtZml4dHVyZQ==",
            ["LocalDatabaseOptions:ConnectionString"] = "Server=prod-sql; Database=FastBypass; User Id=svc_bypass; Password=strong-secret; Encrypt=True;",
            ["EmailOptions:Enabled"] = "true",
            ["EmailOptions:Address"] = "mail@example.com",
            ["EmailOptions:Password"] = "real-secret",
        };

    private static IHostEnvironment HostEnvironment(string name) =>
        new StubHostEnvironment(name);

    private string CreateEmptyRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "required-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        temp_roots.Add(root);
        return root;
    }

    public void Dispose()
    {
        foreach (var root in temp_roots)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class StubHostEnvironment(string environment_name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment_name;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
