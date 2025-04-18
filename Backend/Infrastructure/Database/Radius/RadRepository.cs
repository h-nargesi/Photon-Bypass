using PhotonBypass.Domain;
using PhotonBypass.Infra.Database.Dapper;
using PhotonBypass.Infra.Database.Local;

namespace PhotonBypass.Infra.Database.Radius;

class RadRepository<TEntity>(RadDbContext context) : DapperRepository<TEntity>(context), ILocalRepository<TEntity>
    where TEntity : class, IBaseEntity
{
}
