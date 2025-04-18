using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace PhotonBypass.Infra.Database;

public abstract class DapperDbContext(IOptions<IDapperOptions> options)
{
    public IDbConnection CreateConnection()
    {
        var connection = options.Value.ConnectionString;
        return new SqlConnection(connection);
    }
}
