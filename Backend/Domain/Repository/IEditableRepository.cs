namespace PhotonBypass.Domain.Repository;

public interface IEditableRepository<TEntity> where TEntity : IBaseEntity
{
    IDbContext DbContext { get; }

    IEntityEvent<TEntity> Events { get; }

    Task Save(TEntity entity);

    Task Save(IEnumerable<TEntity> entities);

    Task Delete(TEntity entity);

    Task Delete(IEnumerable<TEntity> entities);
}