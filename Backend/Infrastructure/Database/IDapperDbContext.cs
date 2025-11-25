using System.Data;

namespace PhotonBypass.Infra.Database;

public interface IDapperDbContext : IDisposable
{
    public Task Open();

    public IDbConnection Connection { get; }
}
