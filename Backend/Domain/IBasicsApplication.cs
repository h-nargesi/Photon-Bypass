using PhotonBypass.Domain.Model.Basic;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IBasicsApplication
{
    Task<ApiResult<IEnumerable<PriceModel>>> GetPrices();
}
