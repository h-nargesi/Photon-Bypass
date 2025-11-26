using System.Data;

namespace PhotonBypass.Domain.Repository;

public interface ITransactionalRepository
{
    event EventHandler<EntityEventArgs> Changed;

    Task<IDbTransaction> BeginTransactionAsync();
}
