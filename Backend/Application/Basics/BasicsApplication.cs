using Microsoft.Extensions.Options;
using PhotonBypass.Application.Basics.Model;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Static;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Basics;

class BasicsApplication(IPriceRepository price_repo, IOptions<ManagementOptions> options) : IBasicsApplication
{
    private IPriceRepository PriceRepo { get; } = price_repo;
    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<ApiResult<IList<PriceModel>>> GetPrices()
    {
        var prices = await PriceRepo.GetVisibles()
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

    public Task<ApiResult<AppConfigModel>> GetAppConfig()
    {
        return Task.FromResult(ApiResult<AppConfigModel>.Success(new AppConfigModel
        {
            AllowRegistration = Options.Value.AllowRegistration,
        }));
    }
}
