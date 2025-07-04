using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;
using System.Data;
using System.Data.SQLite;

namespace PhotonBypass.Infra.Repository.DbContext;

class LocalDbContext(IOptions<LocalDapperOptions> options) : DapperDbContext()
{
    public override IDbConnection CreateConnection()
    {
        var connection = new SQLiteConnection(options.Value.ConnectionString);
        connection.Open();
        return connection;
    }
}
