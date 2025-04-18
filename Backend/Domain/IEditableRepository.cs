namespace PhotonBypass.Domain;

public interface IEditableRepository<TEntity> where TEntity : class, IBaseEntity
{
    public Task Save(TEntity entity);
}
