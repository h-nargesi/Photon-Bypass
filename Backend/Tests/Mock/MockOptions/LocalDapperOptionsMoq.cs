using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Infra.Repository.DbContext;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Mock.MockOptions;

internal partial class LocalDapperOptionsMoq : Mock<IOptions<LocalDapperOptions>>, IOptionsMoq
{
    private readonly string connection_string;
    private readonly string stripped;
    private readonly string template;

    public LocalDapperOptionsMoq(IConfiguration configuration)
    {
        connection_string = configuration["LocalDatabaseOptions:ConnectionString"]
                                ?? throw new Exception("LocalDatabaseOptions:ConnectionString was not set.");

        // Accept both "Database=FastBypass;" and " Database=FastBypass;".
        // The spaced form used to be the only one the swap recognized, so a
        // string without the space kept every test on the real FastBypass
        // database while the scripts ran against FastBypass_{key}.
        stripped = DatabaseSelector().Replace(connection_string, string.Empty);
        template = DatabaseSelector().Replace(connection_string, "Database={Database};");

        // Scenario builds the database in a temporary scope, then the real host
        // resolves a new options instance. The package name has to travel with
        // configuration so every later scope stays on FastBypass_{key}.
        var package = configuration["LocalDatabaseOptions:PackageDatabase"];
        if (!string.IsNullOrWhiteSpace(package))
            Database = package;

        Setup(options => options.Value).Returns(() => new LocalDapperOptions
        {
            ConnectionString = Database == null
                ? connection_string
                : template.Replace("{Database}", Database, StringComparison.Ordinal)
        });
    }

    public string RawConnectionString => stripped;

    public string? Database { get; set; }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<LocalDapperOptionsMoq>();
        services.AddScoped(p => p.GetRequiredService<LocalDapperOptionsMoq>().Object);
    }

    [GeneratedRegex(@"(?<=^|;)\s*Database\s*=\s*[^;]*;?", RegexOptions.IgnoreCase)]
    private static partial Regex DatabaseSelector();
}
