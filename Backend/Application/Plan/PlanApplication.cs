using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    Lazy<IPermanentUsersRepository> UserRepo,
    Lazy<ITopUpRepository> TopUpRepo,
    Lazy<IPriceCalculator> PriceCalc,
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IRadiusService> RadiusSrv,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IVpnNodeService> VpnNodeSrv,
    Lazy<IJobContext> JobContext)
    : IPlanApplication
{
    private const long BYTE_IN_GIG = 1024 * 1024 * 1024;

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

    public async Task<ApiResult<RenewalResult>> Renewal(string target, PlanType type, int users, int value)
    {
        var result = PriceCalc.Value.CalculatePrice(type, users, value);

        var account = await AccountRepo.Value.GetAccount(target) ??
            throw new UserException("کاربر مورد نظر پیدا نشد!");

        if (account.Balance < result)
        {
            return ApiResult<RenewalResult>.Success(new RenewalResult
            {
                CurrentPrice = account.Balance,
                MoneyNeeds = result - account.Balance,
            });
        }

        var activation = RadiusSrv.Value.ActivePermanentUser(account.Id, account.CloudId, false);
        var fetch_state = UserRepo.Value.GetPlanState(account.PermanentUserId);
        var fetch_user = UserRepo.Value.GetUser(account.PermanentUserId);

        var state = (await fetch_state) ??
            throw new Exception($"Plan state not found for target: {target}");

        var user = (await fetch_user) ??
            throw new Exception($"Permanent User not found: {account.PermanentUserId}");

        var user_is_changed = false;

        if (type == PlanType.Traffic && state.PlanType != PlanType.Traffic)
        {
            if (state.LeftDays > 0)
            {
                throw new UserException("پلن جاری تمام نشده! برای تغییر نوع پلن باید پلن جاری به اتمام برسد.");
            }

            var data_usage = (long)((state.DataUsage ?? 0) * BYTE_IN_GIG);

            await RadiusSrv.Value.UpdateUserDataUsege(account.Username, data_usage);
        }
        else if (type == PlanType.Monthly && state.PlanType != PlanType.Monthly)
        {
            if (state.GigaLeft > 0.5)
            {
                throw new UserException("پلن جاری تمام نشده! برای تغییر نوع پلن باید ترافیک باقی‌مانده کمتر از ۵۱۲ مگابایت برسد.");
            }

            user.FromDate = DateTime.Now;
            user.ToDate = DateTime.Now;

            user_is_changed = true;
        }

        if (users < state.SimultaneousUserCount)
        {
            _ = VpnNodeSrv.Value.CloseConnections(user.Username, state.SimultaneousUserCount.Value - users);
        }

        if (users != state.SimultaneousUserCount)
        {
            var profile = await GetProfile(account.CloudId, type, users);
            if (profile != null && profile.Id != user.ProfileId)
            {
                user.Profile = profile.Name;
                user.ProfileId = profile.Id;

                user_is_changed = true;
            }
        }

        if (user.LastAcceptTime == null || user.LastAcceptTime.Value < DateTime.Now.AddDays(-7))
        {
            var realm = await ServerMngSrv.Value.GetAvalableRealm(user.CloudId);
            if (realm != null && realm.Id != user.RealmId)
            {
                user.Realm = realm.Name;
                user.RealmId = realm.Id;

                await RadiusSrv.Value.SetRestrictedServer(user.Username, realm.RestrictedServerIP);

                user_is_changed = true;
            }
        }

        await activation;

        if (user_is_changed)
        {
            await RadiusSrv.Value.SaveUserBaiscInfo(user);
        }

        account.Balance -= result;
        switch (type)
        {
            case PlanType.Monthly:
                value = DateTime.Now.MonthToDays(value);
                break;
            case PlanType.Traffic:
                value *= 25;
                break;
        }

        var transaction = AccountRepo.Value.BeginTransaction();

        try
        {
            await AccountRepo.Value.Save(account);

            await RadiusSrv.Value.InsertTopUpAndMakeActive(target, type, value);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        _ = ServerMngSrv.Value.CheckUserServerBalance();

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = account.Balance,
            MoneyNeeds = 0,
        });
    }

    private Task<ProfileEntity> GetProfile(int cloud_id, PlanType type, int users)
    {
        throw new NotImplementedException();
    }
}
