using Microsoft.Extensions.Options;
using PhotonBypass.Application.Basics.Model;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Basics;

class BasicsApplication(IPlanRepository plan_repo, IUserGroupRepository group_repo, IOptions<ManagementOptions> options)
    : IBasicsApplication
{
    private IPlanRepository PlanRepo { get; } = plan_repo;

    private IUserGroupRepository GroupRepo { get; } = group_repo;

    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<ApiResult<IList<PriceModel>>> GetPrices()
    {
        // Docs/24 D1: the public home page serves the marketing cards of the
        // default group's website plans only — private-group plans are never
        // disclosed (no title/price probing).
        var default_group = await GroupRepo.GetDefault();

        var plans = await PlanRepo.GetWebsitePlans(default_group.Id);

        var result = plans
            .Select(x => new PriceModel
            {
                Title = x.Title,
                Caption = x.Caption ?? string.Empty,
                Description = (x.Description ?? string.Empty).Split('\n'),
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
