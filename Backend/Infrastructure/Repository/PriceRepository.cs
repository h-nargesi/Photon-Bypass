using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PriceRepository(LocalDbContext context, Lazy<IPriceCalculator> calculator) : EditableRepository<PriceEntity>(context), IPriceRepository
{
    public async Task<IList<PriceEntity>> GetVisibles()
    {
        await OpenAsync();

        var result = await FindAsync(statement => 
            statement.Where($"{nameof(PriceEntity.State)} = 2"));

        return [.. result];
    }
    
    public async Task<IList<PriceEntity>> GetActives()
    {
        await OpenAsync();

        var result = await FindAsync(statement => 
            statement.Where($"{nameof(PriceEntity.State)} >= 1"));

        return [.. result];
    }
}
