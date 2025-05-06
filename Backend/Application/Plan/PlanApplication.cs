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
    Lazy<IUserPlanStateRepository> PlanRepo,
    Lazy<ITopUpRepository> TopUpRepo,
    Lazy<IPriceCalculator> PriceCalc,
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IRadiusService> RadiusSrv,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IVpnNodeService> VpnNodeSrv,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<IProfileRepository> ProfileRepo,
    Lazy<IJobContext> JobContext)
    : IPlanApplication
{
    public async Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target)
    {
        var state = await PlanRepo.Value.GetPlanState(target);

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
            result.RemainsTitle = state.GetRemainsTitle();

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
            result.RemainsTitle = state.GetRemainsTitle();

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
        var state = await PlanRepo.Value.GetPlanState(target);

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

    public async Task<ApiResult<RenewalResult>> Renewal(string target, PlanType type, int count, int value)
    {
        var account = await AccountRepo.Value.GetAccount(target) ??
            throw new UserException("کاربر مورد نظر پیدا نشد!");

        var estimate = PriceCalc.Value.CalculatePrice(type, count, value);

        Log.Information(@"[user: {0}] Plan renewal request:
    account=(user:{0}, balance:{6})
    request=(taget:{1}, type:{2}, count:{3}, value:{4}, estimate:{7})",
            JobContext.Value.Username, target, type.ToString(), count, value, account.Balance, estimate);

        if (account.Balance < estimate)
        {
            Log.Information(@"[user: {0}] Plan renewal low balance:
    renewal=(taget:{1}, balance:{2}, estimate:{3})",
                JobContext.Value.Username, target, account.Balance, estimate);

            return ApiResult<RenewalResult>.Success(new RenewalResult
            {
                CurrentPrice = account.Balance,
                MoneyNeeds = estimate - account.Balance,
            });
        }

        var activation = RadiusSrv.Value.ActivePermanentUser(account.Id, false);
        var fetch_state = PlanRepo.Value.GetPlanState(account.PermanentUserId);
        var fetch_user = UserRepo.Value.GetUser(account.PermanentUserId);

        var state = (await fetch_state) ??
            throw new Exception($"Plan state not found for target: {target}");

        Log.Information(@"[user: {0}] Plan current state
    request=(taget:{1}, type:{2}, count:{3}, value:{4})
    current=(type:{5}, count:{7}, left-days:{6}, left-gigabytes:{8})",
            JobContext.Value.Username, 
            target, type.ToString(), count, value, state.PlanType.ToString(), 
            state.LeftDays, state.SimultaneousUserCount, state.GigaLeft);

        var user = (await fetch_user) ??
            throw new Exception($"Permanent User not found: {account.PermanentUserId}");

        var user_is_changed = false;

        if (type == PlanType.Traffic && state.PlanType != PlanType.Traffic)
        {
            if (state.LeftDays > 0)
            {
                Log.Information(@"[user: {0}] Plan renewal can not change plan type: current plan has not finished
    change=(taget:{1}, type:{2}, to:{3}, left-days:{6})",
                    JobContext.Value.Username, target, state.PlanType.ToString(), type.ToString(), state.LeftDays);

                throw new UserException("پلن جاری تمام نشده! برای تغییر نوع پلن باید پلن جاری به اتمام برسد.");
            }

            var data_usage = (long)(state.DataUsage ?? 0);

            await RadiusSrv.Value.UpdateUserDataUsege(account.Username, data_usage);
        }
        else if (type == PlanType.Monthly && state.PlanType != PlanType.Monthly)
        {
            if (state.GigaLeft > 0.5)
            {
                Log.Information(@"[user: {0}] Plan renewal can not change plan type: current plan has not finished
    change=(taget:{1}, type:{2}, to:{3}, left-gigabytes:{4})",
                    JobContext.Value.Username, target, state.PlanType.ToString(), type.ToString(), state.GigaLeft);

                throw new UserException("پلن جاری تمام نشده! برای تغییر نوع پلن باید ترافیک باقی‌مانده کمتر از ۵۱۲ مگابایت برسد.");
            }

            user.FromDate = DateTime.Now;
            user.ToDate = DateTime.Now;

            user_is_changed = true;
        }

        if (count < state.SimultaneousUserCount)
        {
            Log.Information(@"[user: {0}] Plan renewal change user count: (closing connections)
    change=(taget:{1}, user-count:{2}, to:{3})",
                JobContext.Value.Username, target, state.SimultaneousUserCount, count);

            _ = VpnNodeSrv.Value.CloseConnections(user.Username, state.SimultaneousUserCount.Value - count);
        }

        if (count != state.SimultaneousUserCount)
        {
            var profile = await ProfileRepo.Value.GetProfile(account.CloudId, type, count);
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

        account.WarningTimes = 0;
        account.Balance -= estimate;
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

            var success = await RadiusSrv.Value.InsertTopUpAndMakeActive(user.Id, type, value);

            if (!success)
            {
                throw new Exception("Insert top-up and make active was unsuccessful!");
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            _ = HistoryRepo.Value.Save(new HistoryEntity
            {
                Issuer = JobContext.Value.Username,
                Target = target,
                EventTime = DateTime.Now,
                Title = "تمدید",
                Description = "خطا در تمدید پلن!",
            });

            throw;
        }

        _ = HistoryRepo.Value.Save(new HistoryEntity
        {
            Issuer = JobContext.Value.Username,
            Target = target,
            EventTime = DateTime.Now,
            Title = "تمدید",
            Description = "پلن تمید شد.",
            Unit = type == PlanType.Monthly ? "ماهانه" : "ترافیک",
            Value = value
        });

        _ = ServerMngSrv.Value.CheckUserServerBalance();

        Log.Information("[user: {0}] Plan renewal finished: ({1}, {2}, {3}, {4})",
            JobContext.Value.Username, target, type.ToString(), count, value);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = account.Balance,
            MoneyNeeds = 0,
        });
    }

}
