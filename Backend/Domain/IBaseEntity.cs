namespace PhotonBypass.Domain;

public interface IBaseEntity
{
    public int Id { get; set; }

    DateTime Created { get; set; }
}

public class EntityEventArgs<TEntity>(TEntity entity)
{
    public TEntity Entity => entity;
}

public delegate Task EntityEventHandler<TEntity>(object? sender, EntityEventArgs<TEntity> e) 
    where TEntity : class, IBaseEntity;
