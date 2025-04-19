using PhotonBypass.Application.Basics.Model;
using PhotonBypass.Domain.Static;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Basics;

class BasicsApplication(IPriceRepository PriceRepo) : IBasicsApplication
{
    public async Task<ApiResult<IList<PriceModel>>> GetPrices()
    {
        var prices = await PriceRepo.GetLeatest()
            ?? throw new Exception("Price is not set!");

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
