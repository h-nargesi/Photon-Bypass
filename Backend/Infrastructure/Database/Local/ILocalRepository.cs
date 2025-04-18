using PhotonBypass.Domain;

namespace PhotonBypass.Infra.Database.Local;

public interface ILocalRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IBaseEntity
{
}
