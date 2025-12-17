using System.Data;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public interface IDapperDbContext : IDbContext, IDisposable
{
    public IDbConnection Connection { get; }

    public Task OpenAsync();
}