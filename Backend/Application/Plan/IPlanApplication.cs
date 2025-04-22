using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target);

    ApiResult<int> Estimate(PlanType type, int users, int value);

    Task<ApiResult<RenewalResult>> Renewal(string target, PlanType type, int users, int value);
}
