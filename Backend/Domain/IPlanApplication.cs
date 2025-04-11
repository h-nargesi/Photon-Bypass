using PhotonBypass.Domain.Model.Plan;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IPlanApplication
{
    Task<ApiResult<UserPlanInfo>> GetPlanState(string target);

    Task<ApiResult<PlanInfo>> GetPlanInfo(string target);

    Task<ApiResult<long>> Estimate(RnewalContext context);

    Task<ApiResult<RnewalResult>> Rnewal(RnewalContext context);
}
