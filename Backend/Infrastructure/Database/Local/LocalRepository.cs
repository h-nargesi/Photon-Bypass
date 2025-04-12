using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra.Database.Local;

class LocalRepository<TEntity>(LocalDbContext context) : DapperRepository<TEntity>(context), ILocalRepository<TEntity>
    where TEntity : class, IBaseEntity
{
}
