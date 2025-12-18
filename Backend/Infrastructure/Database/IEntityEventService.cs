using PhotonBypass.Domain;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Infra.Database;

public interface IEntityEventService
{
    void RegisterOnSave<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity;

    void RegisterOnDelete<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity;

    void UnregisterOnSave<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity;

    void UnregisterOnDelete<TEntity>(EntityEventHandler<TEntity> function) where TEntity : IBaseEntity;

    Task CallOnSave<TEntity>(object? sender, EntityEventArgs<TEntity> event_args) where TEntity : IBaseEntity;

    Task CallOnDelete<TEntity>(object? sender, EntityEventArgs<TEntity> event_args) where TEntity : IBaseEntity;
}