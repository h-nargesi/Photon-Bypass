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
        exception.Message.Should().Contain("TelegramOptions:BotToken");
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
    public void Validate_ProductionWithRealSecretsAndEnabledEmail_Passes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ProductionValues()).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ProductionWithOnlyTestJwtKey_ThrowsNamingOnlyThatKey()
    {
        var values = ProductionValues();
        values["Issuer:Code"] = CommittedTestSecretFixtures.JwtSigningKey;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Issuer:Code");
        exception.Message.Should().NotContain("LocalDatabaseOptions:ConnectionString");
        exception.Message.Should().NotContain("RadDapperOptions:ConnectionString");
        exception.Message.Should().NotContain("RadiusServiceOptions:Password");
        exception.Message.Should().NotContain("EmailOptions");
    }

    [Fact]
    public void Validate_ProductionWithoutEmailOptions_ThrowsNamingTheEmailKeys()
    {
        var values = ProductionValues();
        values.Remove("EmailOptions:Enabled");
        values.Remove("EmailOptions:Address");
        values.Remove("EmailOptions:Password");

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("EmailOptions:Enabled");
        exception.Message.Should().Contain("EmailOptions:Address");
        exception.Message.Should().Contain("EmailOptions:Password");
        exception.Message.Should().NotContain("Issuer:Code");
    }

    [Fact]
    public void Validate_ProductionWithDisabledEmail_ThrowsNamingEnabledOnly()
    {
        var values = ProductionValues();
        values["EmailOptions:Enabled"] = "false";

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

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

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("EmailOptions:Address");
        exception.Message.Should().Contain("EmailOptions:Password");
        exception.Message.Should().NotContain("EmailOptions:Enabled");
    }

    [Fact]
    public void Validate_ProductionWithTelegramChannelButIncompleteConfig_ThrowsNamingTheMissingKeys()
    {
        var values = ProductionValues();
        values["SocialMediaOptions:Channel"] = "Telegram";
        values["TelegramOptions:BotToken"] = "a-real-bot-token";

        // BotUsername, Proxy:Address and OperationsChatId are missing
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = HostEnvironment("Production");

        var act = () => ProductionSecretsGuard.Validate(configuration, environment);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("TelegramOptions:BotUsername");
        exception.Message.Should().Contain("TelegramOptions:Proxy:Address");
        exception.Message.Should().Contain("TelegramOptions:OperationsChatId");
        exception.Message.Should().NotContain("TelegramOptions:BotToken");
    }

    [Fact]
    public void Validate_ProductionWithTelegramChannelOff_IgnoresTheChannelBlock()
    {
        var values = ProductionValues();
        values["SocialMediaOptions:Channel"] = "None";

        // the channel is off: an incomplete telegram block is irrelevant
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
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

            // a well-formed email transport: the email rules enforce
            // presence/enabled, they have no committed-fixture detection
            ["EmailOptions:Enabled"] = "true",
            ["EmailOptions:Address"] = "info@website.com",
            ["EmailOptions:Password"] = "password",

            ["TelegramOptions:BotToken"] = CommittedTestSecretFixtures.TelegramBotToken,
        };

    private static Dictionary<string, string?> ProductionValues() =>
        new()
        {
            ["Issuer:Code"] = "cHJvZHVjdGlvbi1vbmx5LXNpZ25pbmcta2V5LW5vdC1hLXRlc3QtZml4dHVyZQ==",
            ["LocalDatabaseOptions:ConnectionString"] = "Server=prod-sql; Database=FastBypass; User Id=svc_bypass; Password=strong-secret; Encrypt=True;",
            ["RadDapperOptions:ConnectionString"] = "Server=prod-radius-db;Database=production_radius;User=radius;Password=strong-secret;",
            ["RadiusServiceOptions:Password"] = "a-real-radius-api-password",
            ["EmailOptions:Enabled"] = "true",
            ["EmailOptions:Address"] = "mail@example.com",
            ["EmailOptions:Password"] = "real-secret",
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
