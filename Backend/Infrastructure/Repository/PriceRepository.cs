using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PriceRepository(LocalDbContext context) : EditableRepository<PriceEntity>(context), IPriceRepository
{
    public async Task<IList<PriceEntity>> GetLatest()
    {
        var result = await FindAsync(statement => statement.Where($"{nameof(PriceEntity.IsActive)} = 1"));

        return [.. result];
    }
}
