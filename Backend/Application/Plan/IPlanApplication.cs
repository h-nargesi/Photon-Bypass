using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    static event Func<RenewalEvent, bool>? OnRenewal;

    Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target);

    ApiResult<int> Estimate(int users, int months, int gigabytes);

    Task<ApiResult> TemporaryRenewal(string target, int months, int gigabytes);

    Task<ApiResult<RenewalResult>> Renewal(string target, int users, int months, int gigabytes);

    protected static bool OnRenewalDelegation(RenewalEvent arg)
    {
        return OnRenewal == null || OnRenewal.GetInvocationList()
            .Cast<Func<RenewalEvent, bool>>()
            .Select(check_on_renewal => check_on_renewal(arg)).All(result => result);
    }
}

