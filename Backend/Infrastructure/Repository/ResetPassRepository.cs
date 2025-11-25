using Dapper.FastCrud;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class ResetPassRepository(LocalDbContext context) : EditableRepository<ResetPassEntity>(context), IResetPassRepository
{
    public async Task<ResetPassEntity?> GetAccount(string hash_code)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(ResetPassEntity.HashCode)} = @hash_code")
            .WithParameters(new { hash_code }));

        var entity = result.FirstOrDefault();

        if (entity != null)
        {
            _ = Connection.DeleteAsync(entity);
        }

        return entity;
    }

    public async Task AddHashCode(ResetPassEntity hash_code)
    {
        await OpenAsync();

        await Connection.InsertAsync(hash_code);
    }
}
