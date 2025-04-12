using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra.Database;

internal class RadRepository<TEntity, TDtoEntity>(LocalDbContext context) : DapperRepository<TEntity, TDtoEntity>(context)
    where TEntity : class, IBaseEntity where TDtoEntity : class
{
}
