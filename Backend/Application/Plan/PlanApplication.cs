using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain.Profile;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    Lazy<IPermanentUsersRepository> UserRepo)
    : IPlanApplication
{
    public async Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target)
    {
        var state = await UserRepo.Value.GetPlanState(target) ??
            throw new UserException("کاربر مورد نظر پیدا نشد!");

        var result = new UserPlanInfoModel
        {
            Type = state.ResetTypeData == "never" ? PlanType.Traffic : PlanType.Monthly,
            SimultaneousUserCount = state.SimultaneousUserCount,
        };

        if (result.Type == PlanType.Traffic)
        {
            result.RemainsTitle = $"{state.GigaLeft} گیگ باقی مانده";
        }
        else
        {
            if (state.LeftDays > 0)
            {
                result.RemainsTitle = $"و {state.LeftDays} روز";
            }

            if (state.LeftHours > 0 || state.LeftDays <= 0)
            {
                result.RemainsTitle = $"و {state.LeftHours} ساعت";
            }

            result.RemainsTitle = $"{result.RemainsTitle[2..]} باقی مانده";
        }

        return ApiResult<UserPlanInfoModel>.Success(result);
    }

    public Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult<long>> Estimate(RenewalContext context)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResult<RenewalResult>> Renewal(RenewalContext context)
    {
        throw new NotImplementedException();
    }
}
