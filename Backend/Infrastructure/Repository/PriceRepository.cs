using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PriceRepository(LocalDbContext context) : EditableRepository<PriceEntity>(context), IPriceRepository
{
    public async Task<List<PriceEntity>> GetVisibles()
    {
        await OpenAsync();

        var result = await FindAsync(statement =>
            statement.Where($"{nameof(PriceEntity.State)} = 2"));

        return [.. result];
    }

    public async Task<List<PriceEntity>> GetActives()
    {
        await OpenAsync();

        var result = await FindAsync(statement =>
            statement.Where($"{nameof(PriceEntity.State)} >= 1"));

        return [.. result];
    }
}
