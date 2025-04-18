using PhotonBypass.Domain;

namespace PhotonBypass.Infra.Database;

public abstract class EditableRepository<TEntity>(IRepository<TEntity> repository)
    where TEntity : class, IBaseEntity
{
    protected readonly IRepository<TEntity> repository = repository;

    public Task Save(TEntity entity)
    {
        if (entity.Id > 0)
        {
            return repository.UpdateAsync(entity);
        }
        else
        {
            return repository.AddAsync(entity);
        }
    }

}
