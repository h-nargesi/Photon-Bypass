using PhotonBypass.Domain;

namespace PhotonBypass.Infra.Database.Radius;

public interface IRadRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IBaseEntity
{
}
