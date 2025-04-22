using Dapper.FastCrud;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public abstract class EditableRepository<TEntity>(DapperDbContext context) : DapperRepository<TEntity>(context), IEditableRepository<TEntity>
    where TEntity : class, IBaseEntity
{
    private const int UPDATE_MAX_TAKS_COUNT = 10;

    public Task Save(TEntity entity)
    {
        if (entity.Id > 0)
        {
            return connection.UpdateAsync(entity);
        }
        else
        {
            return connection.InsertAsync(entity);
        }
    }

    public async Task BachSave(IEnumerable<TEntity> entities)
    {
        var buffer = new Queue<Task>();

        foreach (var entity in entities)
        {
            if (entity.Id > 0)
            {
                buffer.Enqueue(connection.UpdateAsync(entity));
                if (buffer.Count >= UPDATE_MAX_TAKS_COUNT)
                    await buffer.Dequeue();
            }
            else
            {
                await connection.InsertAsync(entity);
            }
        }

        Task.WaitAll([.. buffer]);
    }

}
