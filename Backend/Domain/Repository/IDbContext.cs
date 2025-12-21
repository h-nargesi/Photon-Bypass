namespace PhotonBypass.Domain.Repository;

public interface IDbContext
{
    Task BeginTransactionAsync();

    Task CommitAsync();

    Task RollbackAsync();
}