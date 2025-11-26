namespace PhotonBypass.Domain.Repository;

public interface IEditableRepository<in TEntity> : ITransactionalRepository where TEntity : class, IBaseEntity
{
    event EventHandler<EntityEventArgs> Changed;

    public Task Save(TEntity entity);

    public Task BachSave(IEnumerable<TEntity> entities);
}
