using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using PhotonBypass.Infra.Database;
using System.Data;

namespace PhotonBypass.FreeRadius.Repository.DbContext;

class RadDbContext(IOptions<RadDapperOptions> options) : DapperDbContext()
{
    public override IDbConnection CreateConnection()
    {
        var connection = new MySqlConnection(options.Value.ConnectionString);
        connection.Open();
        return connection;
    }
}
