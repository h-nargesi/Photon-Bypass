using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using PhotonBypass.Infra.Database;
using System.Data;

namespace PhotonBypass.FreeRadius.Repository.DbContext;

class RadDbContext(IOptions<RadDapperOptions> options) : IDapperDbContext
{
    private readonly MySqlConnection connection = new(options.Value.ConnectionString);

    public IDbConnection Connection => connection;

    public Task OpenAsync()
    {
        return connection.State == ConnectionState.Open ? Task.CompletedTask : connection.OpenAsync();
    }

    public async Task<IDbTransaction> BeginTransactionAsync()
    {
        await OpenAsync();
        return await connection.BeginTransactionAsync();
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
