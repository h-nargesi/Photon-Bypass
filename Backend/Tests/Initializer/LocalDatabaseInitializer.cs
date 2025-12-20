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
        options.Database = "FastBypass_" + key;

        await connection.OpenAsync();
        var structures = DatabaseScriptPrepare.ReplaceDatabaseName(key, await loading_structure_files);

        var loading_data_files =
            SqlFileDependencyHelper.GetSortedFiles(LocalDapperOptionsMoq.DatabaseDataInitializerFilePath + key);

        foreach (var sql in structures)
            await connection.ExecuteAsync(sql);

        var data = DatabaseScriptPrepare.ReplaceDatabaseName(key, await loading_data_files);

        foreach (var sql in data)
            await connection.ExecuteAsync(sql);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<LocalDatabaseInitializer>();
    }
}