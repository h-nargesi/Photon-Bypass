using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using PhotonBypass.Portal.Basical;

namespace PhotonBypass.Test.Facts.Portal;

public class PleskSecretsTest : IDisposable
{
    private readonly string temp_root = Path.Combine(Path.GetTempPath(), "plesk-secrets-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddPleskPrivateSecrets_FileBesideContentRootParent_OverridesAppsettingsPerKey()
    {
        var app_root = CreateAppRoot("vhost/httpdocs");
        WriteJson(Path.Combine(temp_root, "vhost", "httpdocs", "appsettings.json"), new Dictionary<string, object?>
        {
            ["Issuer"] = new Dictionary<string, object?> { ["Code"] = "appsettings-value", ["Name"] = "appsettings-name" }
        });
        WriteJson(Path.Combine(temp_root, "vhost", "private", "secrets.json"), new Dictionary<string, object?>
        {
            ["Issuer"] = new Dictionary<string, object?> { ["Code"] = "secret-value" }
        });

        var builder = CreateBuilder(app_root);

        builder.AddPleskPrivateSecrets();

        builder.Configuration["Issuer:Code"].Should().Be("secret-value");
        builder.Configuration["Issuer:Name"].Should().Be("appsettings-name");
    }

    [Fact]
    public void AddPleskPrivateSecrets_AppNestedOneLevelDown_SecondCandidateIsUsed()
    {
        var app_root = CreateAppRoot("vhost/httpdocs/app");
        WriteJson(Path.Combine(temp_root, "vhost", "private", "secrets.json"), new Dictionary<string, object?>
        {
            ["RadiusServiceOptions:Password"] = "nested-secret"
        });

        var builder = CreateBuilder(app_root);

        builder.AddPleskPrivateSecrets();

        builder.Configuration["RadiusServiceOptions:Password"].Should().Be("nested-secret");
    }

    [Fact]
    public void AddPleskPrivateSecrets_BothCandidatesExist_FirstOneWins()
    {
        var app_root = CreateAppRoot("vhost/httpdocs/app");
        WriteJson(Path.Combine(temp_root, "vhost", "httpdocs", "private", "secrets.json"), new Dictionary<string, object?>
        {
            ["Issuer:Code"] = "first-candidate"
        });
        WriteJson(Path.Combine(temp_root, "vhost", "private", "secrets.json"), new Dictionary<string, object?>
        {
            ["Issuer:Code"] = "second-candidate"
        });

        var builder = CreateBuilder(app_root);

        builder.AddPleskPrivateSecrets();

        builder.Configuration["Issuer:Code"].Should().Be("first-candidate");
    }

    [Fact]
    public void AddPleskPrivateSecrets_NoCandidateFile_IsNoOp()
    {
        var app_root = CreateAppRoot("vhost/httpdocs");
        WriteJson(Path.Combine(temp_root, "vhost", "httpdocs", "appsettings.json"), new Dictionary<string, object?>
        {
            ["Issuer"] = new Dictionary<string, object?> { ["Code"] = "appsettings-value" }
        });

        var builder = CreateBuilder(app_root);

        builder.AddPleskPrivateSecrets();

        builder.Configuration["Issuer:Code"].Should().Be("appsettings-value");
    }

    private string CreateAppRoot(string relative_root)
    {
        var app_root = Path.Combine(temp_root, relative_root.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(app_root);
        return app_root;
    }

    private static WebApplicationBuilder CreateBuilder(string app_root) =>
        WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = app_root,
            EnvironmentName = "Development"
        });

    private static void WriteJson(string path, Dictionary<string, object?> content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(content));
    }

    public void Dispose()
    {
        if (Directory.Exists(temp_root))
        {
            Directory.Delete(temp_root, recursive: true);
        }
    }
}
