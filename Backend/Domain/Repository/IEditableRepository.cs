namespace PhotonBypass.Domain.Repository;

public interface IEditableRepository<TEntity> where TEntity : IBaseEntity
{
    IDbContext DbContext { get; }

    IEntityEvent<TEntity> Events { get; }

    public Task Save(TEntity entity);

    public Task Save(IEnumerable<TEntity> entities);

    Task Delete(TEntity entity);
}