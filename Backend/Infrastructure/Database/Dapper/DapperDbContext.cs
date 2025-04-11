using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace PhotonBypass.Infra.Database.Dapper;

class DapperDbContext(IOptions<DapperOptions> options)
{
    private readonly IOptions<DapperOptions> options = options;

    public IDbConnection CreateConnection()
    {
        var connection = options.Value.ConnectionString;
        return new SqlConnection(connection);
    }
}
