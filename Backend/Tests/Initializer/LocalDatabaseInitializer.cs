using Dapper;
using Microsoft.Data.SqlClient;
using PhotonBypass.Sql;
using PhotonBypass.Test.Mock.MockOptions;
using PhotonBypass.Test.Tools;

namespace PhotonBypass.Test.Initializer;

internal class LocalDatabaseInitializer(LocalDapperOptionsMoq options) : IOutSourceInitializer, IOutSourceLevelService
{
    public async Task Initialize(string key)
    {
        var loading_structure_files =
            SqlFileDependencyHelper.GetSortedFiles(LocalDapperOptionsMoq.DatabaseStructureInitializerFilePath);

        await using var connection = new SqlConnection(options.Object.Value.ConnectionString);
        _ = Check(key);

        await connection.OpenAsync();
        var structures = DatabaseScriptPrepare.ReplaceDatabaseName(key, await loading_structure_files);

        var loading_data_files =
            SqlFileDependencyHelper.GetSortedFiles(LocalDapperOptionsMoq.DatabaseDataInitializerFilePath + key);

        await connection.ExecuteAsync("DROP DATABASE IF EXISTS " + options.Database);
        foreach (var script in structures.SelectMany(x => x.Split("GO")).Where(s => !string.IsNullOrWhiteSpace(s)))
            await connection.ExecuteAsync(script);

        var data = DatabaseScriptPrepare.ReplaceDatabaseName(key, await loading_data_files);

        foreach (var sql in data.Where(s => !string.IsNullOrWhiteSpace(s)))
            await connection.ExecuteAsync(sql);

        _ = Check(key);
    }

    public Task Check(string key)
    {
        options.Database = "FastBypass_" + key;
        return Task.CompletedTask;
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<LocalDatabaseInitializer>();
    }
}