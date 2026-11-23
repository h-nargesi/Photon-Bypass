using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PhotonBypass.Sql;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Test.Mock.MockOptions;
using PhotonBypass.Test.Tools;

namespace PhotonBypass.Test.Initializer;

internal class LocalDatabaseInitializer(LocalDapperOptionsMoq options, IConfiguration configuration)
    : IOutSourceInitializer, IOutSourceLevelService
{
    private readonly string rawConnectionString = options.RawConnectionString;

    public Task Initialize(string key) => Initialize(key, key);

    /// <param name="key">Seed folder under <c>Data/Sql</c>. Also the catalog suffix when <paramref name="catalogSuffix"/> is omitted.</param>
    /// <param name="catalogSuffix">
    /// Catalog name after <c>FastBypass_</c>. Scenario fixtures pass a unique
    /// suffix so parallel classes do not share a database, while still loading
    /// the <c>DbScenario</c> seed.
    /// </param>
    public async Task Initialize(string key, string catalogSuffix)
    {
        if (string.IsNullOrWhiteSpace(catalogSuffix))
            throw new Exception("Catalog suffix was not set.");

        options.Database = "FastBypass_" + catalogSuffix;

        var structure_files_path = configuration["LocalDatabaseOptions:StructureFilesPath"]
                                   ?? throw new Exception("LocalDatabaseOptions:StructureFilesPath was not set.");

        var data_files_path = configuration["LocalDatabaseOptions:DataFilesPath"]
                              ?? throw new Exception("LocalDatabaseOptions:DataFilesPath was not set.");

        // The configured path is relative to the test output. Walk parents too,
        // so a run whose working directory is the repo still finds the scripts.
        var structure_dir = ResolveSqlDirectory(structure_files_path);
        // Some packages (DbTest2, XuiLive) create the schema and insert from
        // the test itself. A missing seed folder is an empty script list.
        var data_dir = TryResolveSqlDirectory(data_files_path + key);

        var loading_structure_files =
            SqlFileDependencyHelper.GetSortedFiles(structure_dir);

        await using var connection = new SqlConnection(rawConnectionString);

        await connection.OpenAsync();
        // DROP fails when this session is standing in the catalog being dropped.
        LeavePackageDatabase(connection);

        var structures = DatabaseScriptPrepare.ReplaceDatabaseName(catalogSuffix, await loading_structure_files);
        if (structures.Count == 0)
            throw new Exception($"No structure scripts in {structure_dir}.");

        await DropPackageDatabase(connection);
        foreach (var script in structures.SelectMany(x => x.Split("GO")).Where(s => !string.IsNullOrWhiteSpace(s)))
            await connection.ExecuteAsync(script);

        if (data_dir == null)
            return;

        connection.ChangeDatabase(options.Database
                                  ?? throw new Exception("Test database name was not set."));

        var data = DatabaseScriptPrepare.ReplaceDatabaseName(catalogSuffix, await SqlFileDependencyHelper.GetSortedFiles(data_dir));

        foreach (var sql in data.Where(s => !string.IsNullOrWhiteSpace(s)))
            await connection.ExecuteAsync(sql);
    }

    /// <summary>
    /// Resolves a configured SQL directory. Relative paths are tried at the
    /// current directory and at <see cref="AppContext.BaseDirectory"/>, then
    /// at each parent, so the same appsettings value works from the test
    /// project and from <c>bin/Debug/net8.0</c>.
    /// </summary>
    internal static string ResolveSqlDirectory(string configured) =>
        TryResolveSqlDirectory(configured) ?? throw new Exception($"SQL path not found: {configured}");

    private static string? TryResolveSqlDirectory(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            throw new Exception("SQL path was not set.");

        if (Path.IsPathRooted(configured) && Directory.Exists(configured))
            return Path.GetFullPath(configured);

        var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var candidate = Path.GetFullPath(Path.Combine(dir.FullName, configured));
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    public Task Check(string key)
    {
        options.Database = "FastBypass_" + key;
        return Task.CompletedTask;
    }

    public async Task Clear(string key)
    {
        _ = Check(key);

        await using var connection = new SqlConnection(rawConnectionString);
        await connection.OpenAsync();
        await DropPackageDatabase(connection);
    }

    /// <summary>
    /// Other hosts (the previous scenario class, or a killed test process)
    /// keep pooled sessions in the package catalog. A plain DROP then fails
    /// with "currently in use". Single-user rolls those sessions back first.
    /// </summary>
    private async Task DropPackageDatabase(SqlConnection connection)
    {
        var name = options.Database ?? throw new Exception("Test database name was not set.");
        ValidateDatabaseName(name);

        LeavePackageDatabase(connection);
        await connection.ExecuteAsync(DropScript(name));
    }

    /// <summary>
    /// Drops a package catalog from a fresh session on <c>master</c>.
    /// Used when the fixture host is already gone and its scoped initializer
    /// can no longer be resolved.
    /// </summary>
    internal static async Task DropIfExists(string connectionString, string databaseName)
    {
        ValidateDatabaseName(databaseName);

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(DropScript(databaseName));
    }

    private static void ValidateDatabaseName(string name)
    {
        if (name.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '_'))
            throw new Exception("Refusing to drop a database with an unexpected name.");
    }

    private static string DropScript(string name) => $"""
        IF DB_ID(N'{name}') IS NOT NULL
        BEGIN
            ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{name}];
        END
        """;

    private void LeavePackageDatabase(SqlConnection connection)
    {
        if (string.Equals(connection.Database, options.Database, StringComparison.OrdinalIgnoreCase))
            connection.ChangeDatabase("master");
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<LocalDatabaseInitializer>();
    }
}