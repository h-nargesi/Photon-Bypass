using PhotonBypass.Domain.User;
using PhotonBypass.Infra.Database.Local;

namespace PhotonBypass.Application.Database;

class ResetPassRepository(ILocalRepository<ResetPassEntity> repository)
{
    public async Task<ResetPassEntity?> GetAccount(string hash_code)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(ResetPassEntity.HashCode)} = @hash_code")
            .WithParameters(new { hash_code }));

        var entity = result.FirstOrDefault();

        if (entity != null)
        {
            _ = repository.DeleteAsync(entity);
        }

        return entity;
    }

    public Task AddHashCode(ResetPassEntity hash_code)
    {
        return repository.AddAsync(hash_code);
    }
}
