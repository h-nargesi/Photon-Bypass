using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;
using System.Data;
using System.Data.SQLite;

namespace PhotonBypass.Infra.Repository.DbContext;

class LocalDbContext(IOptions<LocalDapperOptions> options) : IDapperDbContext
{
    private readonly SQLiteConnection connection = new(options.Value.ConnectionString);

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
