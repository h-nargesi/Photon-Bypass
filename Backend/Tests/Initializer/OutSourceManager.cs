using Dapper;
using Microsoft.Data.SqlClient;
using PhotonBypass.Sql;
using PhotonBypass.Test.Mock.MockOptions;
using PhotonBypass.Test.Tools;

namespace PhotonBypass.Test.Initializer;

public class OutSourceManager(string key)
{
    private bool isInitialized;
    private readonly SemaphoreSlim semaphore = new(0);
    public readonly List<Exception> Exceptions = [];

    public async Task<OutSourceManager> Initialize()
    {
        if (isInitialized) return this;

        await semaphore.WaitAsync();

        if (isInitialized) return this;

        await Task.WhenAll(
            InitializeLocalDatabase(),
            InitializeMikrotik());

        if (Exceptions.Count > 0)
        {
            throw new Exception("Some error happened in initialization.");
        }

        isInitialized = true;
        semaphore.Release(int.MaxValue);
        return this;
    }

    private async Task InitializeLocalDatabase()
    {
        try
        {
            var loading_structure_files =
                SqlFileDependencyHelper.GetSortedFiles(LocalDapperOptionsMoq.DatabaseStructureInitializerFilePath);

            var options = new LocalDapperOptionsMoq().Object;
            await using var connection = new SqlConnection(options.Value.ConnectionString);

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
        catch (Exception exception)
        {
            lock (Exceptions)
            {
                Exceptions.Add(exception);
            }
        }
    }

    private async Task InitializeMikrotik()
    {
        if (key.StartsWith("DbTest")) return;
    }
}