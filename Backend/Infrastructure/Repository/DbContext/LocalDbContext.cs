using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;
using System.Data;
using System.Data.SQLite;

namespace PhotonBypass.Infra.Repository.DbContext;

class LocalDbContext(IOptions<LocalDapperOptions> options) : IDapperDbContext, IDisposable
{
    public SQLiteConnection Connection { get; } = new SQLiteConnection(options.Value.ConnectionString);

    IDbConnection IDapperDbContext.Connection => Connection;

    public Task Open()
    {
        if (Connection.State == ConnectionState.Open)
            return Task.CompletedTask;

        return Connection.OpenAsync();
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}
