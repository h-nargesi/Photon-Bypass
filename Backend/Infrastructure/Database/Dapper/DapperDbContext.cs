using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace PhotonBypass.Infra.Database.Dapper;

abstract class DapperDbContext(IOptions<IDapperOptions> options)
{
    private readonly IOptions<IDapperOptions> options = options;

    public IDbConnection CreateConnection()
    {
        var connection = options.Value.ConnectionString;
        return new SqlConnection(connection);
    }
}
