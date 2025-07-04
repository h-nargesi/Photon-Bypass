using System.Data;

namespace PhotonBypass.Infra.Database;

public abstract class DapperDbContext()
{
    public abstract IDbConnection CreateConnection();
}
