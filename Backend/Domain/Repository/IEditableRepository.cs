namespace PhotonBypass.Domain.Repository;

public interface IEditableRepository<TEntity> : IRepository where TEntity : class, IBaseEntity
{
    public Task Save(TEntity entity);

    public Task BachSave(IEnumerable<TEntity> entities);
}
