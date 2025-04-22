using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Static;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    Lazy<IPermanentUsersRepository> UserRepo,
    Lazy<ITopUpRepository> TopUpRepo,
    Lazy<IPriceCalculator> PriceCalc,
    Lazy<IJobContext> JobContext)
    : IPlanApplication
{
    public async Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target)
    {
        var state = await UserRepo.Value.GetPlanState(target);

        if (state == null)
        {
            return new ApiResult<UserPlanInfoModel>
            {
                Message = "بدون پلن",
            };
        }

        Log.Information("[user: {0}] invalid plan state: (target:{1}, user-count:{2}, type:{3}, data-left:{4}, total-data:{5}, time-left:{6}-{7})",
            JobContext.Value.Username, target, state.SimultaneousUserCount, state.PlanType.ToString(), 
            state.GigaLeft, state.TotalData, state.LeftDays, state.LeftHours);

        var result = new UserPlanInfoModel
        {
            Type = state.PlanType,
            SimultaneousUserCount = state.SimultaneousUserCount,
        };

        var top_up = await TopUpRepo.Value.LatestOf(state.Id);

        if (result.Type == PlanType.Traffic)
        {
            result.RemainsTitle = $"{state.GigaLeft?.ToString() ?? "--"} گیگ باقی مانده";

            if (state.TotalData.HasValue)
            {
                result.RemainsPercent = (int)(100 * (1 - (state.GigaLeft ?? 0) / (top_up?.GigaData ?? state.TotalData)));
            }
            else
            {
                Log.Fatal("[user: {0}] invalid plan state: (target:{1}, user-count:{2}, type:{3}, left:{4}, total:{5})", 
                    JobContext.Value.Username, target, state.SimultaneousUserCount, state.PlanType.ToString(), state.GigaLeft, state.TotalData);
            }
        }
        else
        {
            if (state.LeftDays > 0)
            {
                result.RemainsTitle = $"و {state.LeftDays} روز";
            }

            if (state.LeftHours > 0 || !(state.LeftDays > 0))
            {
                result.RemainsTitle = $"و {state.LeftHours?.ToString() ?? "--"} ساعت";
            }

            result.RemainsTitle = $"{result.RemainsTitle[2..]} باقی مانده";

            if (!state.LeftDays.HasValue && !state.LeftHours.HasValue)
            {
                Log.Fatal("[user: {0}] invalid plan state: (target:{1}, user-count:{2}, type:{3}, time-left:{4}-{5})",
                    JobContext.Value.Username, target, state.SimultaneousUserCount, state.PlanType.ToString(), state.LeftDays, state.LeftHours);
            }
            else if (top_up?.DaysToUse != null)
            {
                var days = state.LeftDays ?? 0 + (state.LeftHours ?? 0) / 24;
                result.RemainsPercent = (int)(100 * (1 - days / top_up.DaysToUse));
            }
        }

        return ApiResult<UserPlanInfoModel>.Success(result);
    }

    public async Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target)
    {
        var state = await UserRepo.Value.GetPlanState(target);

        if (state == null)
        {
            return new ApiResult<PlanInfoModel>
            {
                Message = "بدون پلن",
            };
        }

        var top_up = await TopUpRepo.Value.LatestOf(state.Id);

        return ApiResult<PlanInfoModel>.Success(new PlanInfoModel
        {
            Target = target,
            SimultaneousUserCount = state.SimultaneousUserCount,
            Type = state.PlanType,
            Value = top_up != null ? (state.PlanType == PlanType.Traffic ? (int?)top_up.GigaData : top_up.DaysToUse) : null,
        });
    }

    public ApiResult<int> Estimate(PlanType type, int users, int value)
    {
        var result = PriceCalc.Value.CalculatePrice(type, users, value);
        return ApiResult<int>.Success(result);
    }

    public Task<ApiResult<RenewalResult>> Renewal(string target, PlanType type, int users, int value)
    {
        var result = PriceCalc.Value.CalculatePrice(type, users, value);
        throw new NotImplementedException();
    }
}
