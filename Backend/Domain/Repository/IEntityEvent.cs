namespace PhotonBypass.Domain.Repository;

public interface IEntityEvent<TEntity> where TEntity : IBaseEntity
{
    event EntityEventHandler<TEntity> OnSave;

    event EntityEventHandler<TEntity> OnDelete;
}

public class EntityEventArgs<TEntity>(params TEntity[] entities)
{
    public TEntity[] Entities => entities;
}

public delegate Task EntityEventHandler<TEntity>(object? sender, EntityEventArgs<TEntity> event_args)
    where TEntity : IBaseEntity;