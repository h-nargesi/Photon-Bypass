using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockOptions;

namespace PhotonBypass.Test.Facts.Initializer;

public class LocalDapperOptionsMoqTest
{
    [Theory]
    [InlineData("Server=.;Database=FastBypass;User Id=test;Password=secret;Encrypt=False;")]
    [InlineData("Server=.; Database=FastBypass; User Id=test; Password=secret; Encrypt=False;")]
    public void NamedDatabase_RewritesTheCatalogAndRawStringDropsIt(string connection_string)
    {
        var options = new LocalDapperOptionsMoq(Configuration(connection_string))
        {
            Database = "FastBypass_DbScenario"
        };

        var targeted = options.Object.Value.ConnectionString;

        targeted.Should().Contain("Database=FastBypass_DbScenario;");
        targeted.Should().NotContain("Database=FastBypass;");
        targeted.Should().Contain("User Id=test;");
        options.RawConnectionString.Should().NotContain("Database=");
        options.RawConnectionString.Should().Contain("Server=.;");
    }

    [Fact]
    public void PackageDatabaseInConfiguration_RewritesEveryScope()
    {
        var options = new LocalDapperOptionsMoq(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabaseOptions:ConnectionString"] =
                    "Server=.;Database=FastBypass;User Id=test;Password=secret;",
                ["LocalDatabaseOptions:PackageDatabase"] = "FastBypass_DbScenario",
            })
            .Build());

        var targeted = options.Object.Value.ConnectionString;

        targeted.Should().Contain("Database=FastBypass_DbScenario;");
        targeted.Should().NotContain("Database=FastBypass;");
        options.RawConnectionString.Should().NotContain("Database=");
    }

    [Fact]
    public void StructureAndScenarioSql_ResolveFromTheTestOutputDirectory()
    {
        var structure = LocalDatabaseInitializer.ResolveSqlDirectory("../../Database/LocalDatabase/");
        var scenario = LocalDatabaseInitializer.ResolveSqlDirectory("Data/Sql/DbScenario");

        File.Exists(Path.Combine(structure, "Database.sql")).Should().BeTrue();
        File.Exists(Path.Combine(structure, "Price.sql")).Should().BeTrue();
        File.Exists(Path.Combine(scenario, "init.sql")).Should().BeTrue();
    }

    [Fact]
    public void WithoutAPackage_TheOriginalCatalogStays()
    {
        const string connection_string =
            "Server=.;Database=FastBypass;User Id=test;Password=secret;";

        var options = new LocalDapperOptionsMoq(Configuration(connection_string));

        options.Object.Value.ConnectionString.Should().Be(connection_string);
    }

    private static IConfiguration Configuration(string connection_string) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalDatabaseOptions:ConnectionString"] = connection_string,
            })
            .Build();
}
