using System.Data;

namespace PhotonBypass.Domain.Repository;

public interface ITransactionalRepository
{
    IDbTransaction BeginTransaction();
}
