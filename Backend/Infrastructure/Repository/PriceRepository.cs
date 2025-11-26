using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PriceRepository(LocalDbContext context, Lazy<IPriceCalculator> calculator) : EditableRepository<PriceEntity>(context), IPriceRepository
{
    public async Task<IList<PriceEntity>> GetLatest()
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement.Where($"{nameof(PriceEntity.IsActive)} = 1"));

        return [.. result];
    }

    public override async Task Save(PriceEntity entity)
    {
        await base.Save(entity);
        await calculator.Value.UpdateCalculatorCode();
    }
}
