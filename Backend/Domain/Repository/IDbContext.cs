using System.Data;

namespace PhotonBypass.Domain.Repository;

public interface IDbContext
{
    Task<IDbTransaction> BeginTransactionAsync();
}