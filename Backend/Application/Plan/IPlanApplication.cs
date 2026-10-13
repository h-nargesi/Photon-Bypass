using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    Task<ApiResult<PlanStateModel>> GetPlanState(string target);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target);

    Task<ApiResult<EstimateResult>> Estimate(string target, byte users, short days, int gigabytes);

    Task<ApiResult<RenewalResult>> Renewal(string target, byte users, short days, int gigabytes);

    Task<ApiResult<RenewalResult>> Renewal(int account_id, int? payment_id, string action, int? invoice_code = null);
}
