using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target);

    Task<ApiResult<long>> Estimate(RenewalContext context);

    Task<ApiResult<RenewalResult>> Rnewal(RenewalContext context);
}
