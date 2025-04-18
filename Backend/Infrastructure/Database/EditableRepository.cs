using PhotonBypass.Domain;

namespace PhotonBypass.Infra.Database;

public abstract class EditableRepository<TEntity>(DapperDbContext context) : DapperRepository<TEntity>(context)
    where TEntity : class, IBaseEntity
{
    public Task Save(TEntity entity)
    {
        if (entity.Id > 0)
        {
            return UpdateAsync(entity);
        }
        else
        {
            return AddAsync(entity);
        }
    }
}
