using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Static;

public interface IPriceRepository : IEditableRepository<PriceEntity>
{
    Task<List<PriceEntity>> GetVisibles();
    
    Task<List<PriceEntity>> GetActives();
}
