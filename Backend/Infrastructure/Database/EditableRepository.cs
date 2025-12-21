using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public abstract class EditableRepository<TEntity>(IDapperDbContext context)
    : DapperRepository<TEntity>(context), IEditableRepository<TEntity>
    where TEntity : class, IBaseEntity
{
    private EntityEventHandler? eventHandler;

    public IDbContext DbContext { get; } = context;

    public IEntityEvent<TEntity> Events => eventHandler ??= new EntityEventHandler(DapperDbContext.EventService);

    public virtual async Task Save(TEntity entity)
    {
        await DapperDbContext.OpenAsync();

        if (entity.Id > 0)
        {
            await DapperDbContext.Connection.UpdateAsync(entity, CheckTransaction<TEntity>());
        }
        else
        {
            await DapperDbContext.Connection.InsertAsync(entity, CheckTransaction<TEntity>());
        }

        await DapperDbContext.EventService.CallOnSave(this, new EntityEventArgs<TEntity>(entity));
    }

    public virtual async Task BachSave(IEnumerable<TEntity> entities)
    {
        await DapperDbContext.OpenAsync();

        var entity_list = entities.ToArray();
        var updates = entity_list.Where(entity => entity.Id > 0);
        var inserts = entity_list.Where(entity => entity.Id <= 0);

        await DapperDbContext.Connection.BulkUpdateAsync(updates);

        foreach (var entity in inserts)
        {
            await DapperDbContext.Connection.InsertAsync(entity, CheckTransaction<TEntity>());
        }

        await DapperDbContext.EventService.CallOnSave(this, new EntityEventArgs<TEntity>(entity_list));
    }

    public virtual async Task Delete(TEntity entity)
    {
        await DapperDbContext.OpenAsync();

        await DapperDbContext.Connection.DeleteAsync(entity);

        await DapperDbContext.EventService.CallOnDelete(this, new EntityEventArgs<TEntity>(entity));
    }

    private Action<IStandardSqlStatementOptionsBuilder<T>>? CheckTransaction<T>(
        Action<IStandardSqlStatementOptionsBuilder<T>>? statement = null)
    {
        if (DapperDbContext.CurrentTransaction == null) return statement;
        
        var arg_statement = statement;
        statement = st =>
        {
            arg_statement?.Invoke(st);
            st.AttachToTransaction(DapperDbContext.CurrentTransaction);
        };

        return statement;
    }

    private Action<IConditionalBulkSqlStatementOptionsBuilder<T>>? CheckTransactionBulk<T>(
        Action<IConditionalBulkSqlStatementOptionsBuilder<T>>? statement = null)
    {
        if (DapperDbContext.CurrentTransaction == null) return statement;
        
        var arg_statement = statement;
        statement = st =>
        {
            arg_statement?.Invoke(st);
            st.AttachToTransaction(DapperDbContext.CurrentTransaction);
        };

        return statement;
    }

    private class EntityEventHandler(IEntityEventService entity_event_service) : IEntityEvent<TEntity>
    {
        public event EntityEventHandler<TEntity> OnSave
        {
            add => entity_event_service.RegisterOnSave(value);
            remove => entity_event_service.UnregisterOnSave(value);
        }

        public event EntityEventHandler<TEntity> OnDelete
        {
            add => entity_event_service.RegisterOnDelete(value);
            remove => entity_event_service.UnregisterOnDelete(value);
        }
    }
}