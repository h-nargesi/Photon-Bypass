using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Repository.DbContext;

internal class LocalDbContext(IOptions<LocalDapperOptions> options) : IDapperDbContext
{
    private readonly SqlConnection connection = new(options.Value.ConnectionString);

    public IDbConnection Connection => connection;

    public Task Open()
    {
        return connection.State == ConnectionState.Open ? Task.CompletedTask : connection.OpenAsync();
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
