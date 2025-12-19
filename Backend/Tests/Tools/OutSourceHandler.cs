using Dapper;
using Microsoft.Data.SqlClient;
using PhotonBypass.Sql;
using PhotonBypass.Test.Mock.MockOptions;
using PhotonBypass.Test.MockOptions;

namespace PhotonBypass.Test.Tools;

public static class OutSourceHandler
{
    private static readonly Dictionary<string, OutSource> OutSources = [];

    public static Task<OutSource> Get(string key)
    {
        OutSource? out_source;

        lock (OutSources)
        {
            if (!OutSources.TryGetValue(key, out out_source))
            {
                OutSources.Add(key, out_source = new OutSource(key));
            }
        }

        return out_source.Initialize();
    }

    public class OutSource(string key)
    {
        private bool isInitialized;
        private readonly List<Exception> Exceptions = [];
        private readonly SemaphoreSlim semaphore = new(0);

        public async Task<OutSource> Initialize()
        {
            if (isInitialized) return this;

            await semaphore.WaitAsync();

            if (isInitialized) return this;

            Task.WaitAll(
                InitializeLocalDatabase(),
                InitializeMikrotik());

            if (Exceptions.Count > 0)
            {
                throw new Exception("Some error happened in initialization.", Exceptions[0]);
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
}