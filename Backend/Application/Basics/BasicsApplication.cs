using PhotonBypass.Application.Basics.Model;
using PhotonBypass.Domain.Static;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Basics;

class BasicsApplication(IPriceRepository price_repo) : IBasicsApplication
{
    public async Task<ApiResult<IList<PriceModel>>> GetPrices()
    {
        var prices = await price_repo.GetVisibles()
            ?? throw new Exception("Prices are not set!");

        var result = prices
            .Select(x => new PriceModel
            {
                Title = x.Title,
                Caption = x.Caption,
                Description = x.Description.Split('\n'),
            })
            .ToList();

        return ApiResult<IList<PriceModel>>.Success(result);
    }
}
