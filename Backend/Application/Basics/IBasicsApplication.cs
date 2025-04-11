using PhotonBypass.Application.Basics.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Basics;

public interface IBasicsApplication
{
    Task<ApiResult<IEnumerable<PriceModel>>> GetPrices();
}
