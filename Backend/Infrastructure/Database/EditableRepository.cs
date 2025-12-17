using Dapper.FastCrud;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public abstract class EditableRepository<TEntity>(IDapperDbContext context)
    : DapperRepository<TEntity>(context), IEditableRepository<TEntity>
    where TEntity : class, IBaseEntity
{
    private const int UpdateMaxTasksCount = 10;

    public IDbContext DbContext => DapperDbContext;

    // TODO: make static event for all instances of same type repositories
    public event EntityEventHandler<TEntity>? OnSaved;

    public virtual async Task Save(TEntity entity)
    {
        await DapperDbContext.OpenAsync();

        if (entity.Id > 0)
        {
            await DapperDbContext.Connection.UpdateAsync(entity);
        }
        else
        {
            await DapperDbContext.Connection.InsertAsync(entity);
        }

        if (OnSaved != null)
        {
            await OnSaved(this, new EntityEventArgs<TEntity>(entity));
        }
    }

    public virtual async Task BachSave(IEnumerable<TEntity> entities)
    {
        await DapperDbContext.OpenAsync();
        var buffer = new Queue<Task>();

        foreach (var entity in entities)
        {
            if (entity.Id > 0)
            {
                buffer.Enqueue(DapperDbContext.Connection.UpdateAsync(entity));
                if (buffer.Count >= UpdateMaxTasksCount)
                    await buffer.Dequeue();
            }
            else
            {
                await DapperDbContext.Connection.InsertAsync(entity);
            }
        }

        Task.WaitAll([.. buffer]);
    }

    public virtual async Task Delete(TEntity entity)
    {
        await DapperDbContext.OpenAsync();

        await DapperDbContext.Connection.DeleteAsync(entity);
    }
}