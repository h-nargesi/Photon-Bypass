using PhotonBypass.Domain;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Services;

public class EntityEventService : IEntityEventService
{
    private readonly ReaderWriterLockSlim @lock = new();
    private readonly Dictionary<Type, HashSet<object>> saveEvents = [];
    private readonly Dictionary<Type, HashSet<object>> deleteEvents = [];

    public void RegisterOnSave<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity
    {
        Register(saveEvents, function);
    }

    public void RegisterOnDelete<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity
    {
        Register(deleteEvents, function);
    }

    public void UnregisterOnSave<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity
    {
        Unregister(saveEvents, function);
    }

    public void UnregisterOnDelete<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity
    {
        Unregister(deleteEvents, function);
    }

    public Task CallOnSave<TEntity>(object? sender, EntityEventArgs<TEntity> event_args) where TEntity : IBaseEntity
    {
        return Call(saveEvents, sender, event_args);
    }

    public Task CallOnDelete<TEntity>(object? sender, EntityEventArgs<TEntity> event_args) where TEntity : IBaseEntity
    {
        return Call(deleteEvents, sender, event_args);
    }

    private void Register<TEntity>(Dictionary<Type, HashSet<object>> events, EntityEventHandler<TEntity> function)
        where TEntity : IBaseEntity
    {
        try
        {
            @lock.EnterWriteLock();
            if (!events.TryGetValue(typeof(TEntity), out var entity_event_list))
            {
                events.Add(typeof(TEntity), entity_event_list = []);
            }

            entity_event_list.Add(function);
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    private void Unregister<TEntity>(Dictionary<Type, HashSet<object>> events, EntityEventHandler<TEntity> function)
        where TEntity : IBaseEntity
    {
        try
        {
            @lock.EnterWriteLock();
            if (!events.TryGetValue(typeof(TEntity), out var entity_event_list))
            {
                return;
            }

            entity_event_list.Remove(function);
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    public Task Call<TEntity>(Dictionary<Type, HashSet<object>> events, object? sender,
        EntityEventArgs<TEntity> event_args) where TEntity : IBaseEntity
    {
        Task[] tasks;

        try
        {
            @lock.EnterReadLock();
            if (!events.TryGetValue(typeof(TEntity), out var entity_event_list))
            {
                return Task.CompletedTask;
            }

            tasks = entity_event_list.Select(e => e as EntityEventHandler<TEntity>)
                .Where(e => e != null)
                .Select(e => e!.Invoke(sender, event_args))
                .ToArray();
        }
        finally
        {
            @lock.ExitReadLock();
        }

        return tasks.Length <= 0 ? Task.CompletedTask : Task.WhenAll(tasks);
    }
}