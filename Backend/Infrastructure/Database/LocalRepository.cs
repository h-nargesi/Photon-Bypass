using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra.Database;

public class LocalRepository<TEntity>(LocalDbContext context) : DapperRepository<TEntity, TEntity>(context)
    where TEntity : class, IBaseEntity
{
}
