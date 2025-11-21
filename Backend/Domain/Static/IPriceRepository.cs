using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Static;

public interface IPriceRepository : IEditableRepository<PriceEntity>
{
    Task<IList<PriceEntity>> GetLatest();
}
