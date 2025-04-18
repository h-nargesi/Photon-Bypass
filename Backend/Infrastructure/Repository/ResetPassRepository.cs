using PhotonBypass.Domain.Account;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class ResetPassRepository(LocalDbContext context) : EditableRepository<ResetPassEntity>(context), IResetPassRepository
{
    public async Task<ResetPassEntity?> GetAccount(string hash_code)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(ResetPassEntity.HashCode)} = @hash_code")
            .WithParameters(new { hash_code }));

        var entity = result.FirstOrDefault();

        if (entity != null)
        {
            _ = DeleteAsync(entity);
        }

        return entity;
    }

    public Task AddHashCode(ResetPassEntity hash_code)
    {
        return AddAsync(hash_code);
    }
}
