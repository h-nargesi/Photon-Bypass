using System.Data;

namespace PhotonBypass.Domain.Repository;

public interface IRepository
{
    IDbTransaction BeginTransaction();
}
