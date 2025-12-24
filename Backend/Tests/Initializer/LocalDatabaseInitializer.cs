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

    public async Task Initialize(string key)
    {
        _ = Check(key);

        var structure_files_path = configuration["LocalDatabaseOptions:StructureFilesPath"]
                                   ?? throw new Exception("LocalDatabaseOptions:StructureFilesPath was not set.");

        var data_files_path = configuration["LocalDatabaseOptions:DataFilesPath"]
                              ?? throw new Exception("LocalDatabaseOptions:DataFilesPath was not set.");

        var loading_structure_files =
            SqlFileDependencyHelper.GetSortedFiles(structure_files_path);

        await using var connection = new SqlConnection(rawConnectionString);

        await connection.OpenAsync();
        var structures = DatabaseScriptPrepare.ReplaceDatabaseName(key, await loading_structure_files);

        var loading_data_files =
            SqlFileDependencyHelper.GetSortedFiles(data_files_path + key);

        await connection.ExecuteAsync("DROP DATABASE IF EXISTS " + options.Database);
        foreach (var script in structures.SelectMany(x => x.Split("GO")).Where(s => !string.IsNullOrWhiteSpace(s)))
            await connection.ExecuteAsync(script);

        var data = DatabaseScriptPrepare.ReplaceDatabaseName(key, await loading_data_files);

        foreach (var sql in data.Where(s => !string.IsNullOrWhiteSpace(s)))
            await connection.ExecuteAsync(sql);
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
        await connection.ExecuteAsync("DROP DATABASE IF EXISTS " + options.Database);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<LocalDatabaseInitializer>();
    }
}