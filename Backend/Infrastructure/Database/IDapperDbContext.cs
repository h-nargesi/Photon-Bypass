using System.Data;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public interface IDapperDbContext : IDbContext, IDisposable
{
    public IDbConnection Connection { get; }

    IEntityEventService EventService { get; }

    public Task OpenAsync();
}