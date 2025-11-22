using PhotonBypass.Application.Plan.Model;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPlanApplication
{
    static event Func<RenewalEvent, bool>? OnRenewal;

    Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target);

    Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target);

    ApiResult<int> Estimate(PlanType type, int users, int value);

    Task<ApiResult> TemporaryRenewal(string target, PlanType type);

    Task<ApiResult<RenewalResult>> Renewal(string target, PlanType type, int users, int value);

    protected static bool OnRenewalDelegation(RenewalEvent arg)
    {
        if (OnRenewal == null) return true;

        foreach (Func<RenewalEvent, bool> checkOnReneal in OnRenewal.GetInvocationList().Cast<Func<RenewalEvent, bool>>())
        {
            var result = checkOnReneal(arg);

            if (!result) return false;
        }

        return true;
    }
}
