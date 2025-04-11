using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    Task<ApiResult<UserPlanInfo>> GetPlanState(string target);

    Task<ApiResult<PlanInfo>> GetPlanInfo(string target);

    Task<ApiResult<long>> Estimate(RnewalContext context);

    Task<ApiResult<RnewalResult>> Rnewal(RnewalContext context);
}
