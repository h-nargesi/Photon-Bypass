﻿namespace PhotonBypass.Domain.Repository;

public interface IEditableRepository<TEntity> : ITransactionalRepository where TEntity : class, IBaseEntity
{
    public Task Save(TEntity entity);

    public Task BachSave(IEnumerable<TEntity> entities);
}
