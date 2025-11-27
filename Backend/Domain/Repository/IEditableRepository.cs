namespace PhotonBypass.Domain.Repository;

public interface IEditableRepository<TEntity> : ITransactionalRepository where TEntity : class, IBaseEntity
{
    event EntityEventHandler<TEntity> OnSaved;

    public Task Save(TEntity entity);

    public Task BachSave(IEnumerable<TEntity> entities);
}
