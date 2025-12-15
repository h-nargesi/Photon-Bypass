using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    static event Func<RenewalEvent, bool>? OnRenewal;

    Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target);

    Task<ApiResult<int>> Estimate(string target, byte users, short days, int gigabytes);

    Task<ApiResult<RenewalResult>> Renewal(string target, byte users, short days, int gigabytes);

    protected static bool OnRenewalDelegation(RenewalEvent arg)
    {
        return OnRenewal == null || OnRenewal.GetInvocationList()
            .Cast<Func<RenewalEvent, bool>>()
            .Select(check_on_renewal => check_on_renewal(arg)).All(result => result);
    }
}

