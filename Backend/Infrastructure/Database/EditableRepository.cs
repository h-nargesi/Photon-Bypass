using System.Data;
using Dapper.FastCrud;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public abstract class EditableRepository<TEntity>(IDapperDbContext context) : DapperRepository<TEntity>(context), IEditableRepository<TEntity>
    where TEntity : class, IBaseEntity
{
    private const int UPDATE_MAX_TAKS_COUNT = 10;

    public async Task<IDbTransaction> BeginTransactionAsync()
    {
        await OpenAsync();
        return Connection.BeginTransaction();
    }

    public async Task Save(TEntity entity)
    {
        await OpenAsync();
        if (entity.Id > 0)
        {
            await Connection.UpdateAsync(entity);
        }
        else
        {
            await Connection.InsertAsync(entity);
        }
    }

    public async Task BachSave(IEnumerable<TEntity> entities)
    {
        await OpenAsync();
        var buffer = new Queue<Task>();

        foreach (var entity in entities)
        {
            if (entity.Id > 0)
            {
                buffer.Enqueue(Connection.UpdateAsync(entity));
                if (buffer.Count >= UPDATE_MAX_TAKS_COUNT)
                    await buffer.Dequeue();
            }
            else
            {
                await Connection.InsertAsync(entity);
            }
        }

        Task.WaitAll([.. buffer]);
    }
}
