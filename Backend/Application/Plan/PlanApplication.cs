using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    Lazy<IPermanentUsersRepository> UserRepo,
    Lazy<ISessionStateRepository> SessionRepo,
    Lazy<IRenewalRepository> RenewalRepo,
    Lazy<IPriceCalculator> PriceCalc,
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IRadiusService> RadiusSrv,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IVpnNodeService> VpnNodeSrv,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<IProfileRepository> ProfileRepo,
    Lazy<INasRepository> NasRepo,
    Lazy<IJobContext> JobContext)
    : IPlanApplication
{
    public async Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target)
    {
        var renew = await RenewalRepo.Value.LatestOf(target);

        if (renew == null)
        {
            return new ApiResult<UserPlanInfoModel>
            {
                Message = "بدون پلن",
            };
        }

        var state = await SessionRepo.Value.GetSessionState(renew.AccountId);

        if (state == null)
        {
            return new ApiResult<UserPlanInfoModel>
            {
                Message = "بدون مصرف",
            };
        }

        Log.Information("[user: {0}] session state: (target:{1}, user-count:{2}, data-left:{3}, total-data:{4}, time-left:{5}-{6})",
            JobContext.Value.Username, target, state.SimultaneousUserCount,
            state.GetTrafficLeftInGig(), state.GetTrafficLimitInGig(), state.TimeLeft?.TotalDays, state.TimeLeft?.Hours);

        var result = new UserPlanInfoModel
        {
            RemainsTitle = state.GetRemainsTitle(),
            SimultaneousUserCount = state.SimultaneousUserCount,
        };

        if (renew.TrafficLimit.HasValue)
        {
            if (state.TrafficLeft.HasValue)
            {
                result.RemainsTrafficPercent = (int)(100 * (1 - state.TrafficLeft.Value / renew.TrafficLimit.Value));
            }
            else
            {
                Log.Fatal("[user: {0}] invalid sesstion state: (target:{1}, user-count:{2}, traffic-limit:{3})",
                    JobContext.Value.Username, target, renew.SimultaneousUse, renew.GetTrafficLimitInGig());
            }
        }        

        if (renew.MonthLimit.HasValue)
        {
            if (state.TimeLeft.HasValue && state.ExpirationDate.HasValue)
            {
                var time_limit = state.ExpirationDate.Value - state.ExpirationDate.Value.AddPersianMonth(-renew.MonthLimit.Value);
                result.RemainsTimePercent = (int)(100 * (1 - state.TimeLeft.Value.TotalMinutes / time_limit.TotalMinutes));
            }
            else
            {
                Log.Fatal("[user: {0}] invalid plan state: (target:{1}, user-count:{2}, time-limit:{3})",
                    JobContext.Value.Username, target, renew.SimultaneousUse, renew.MonthLimit);
            }
        }

        return ApiResult<UserPlanInfoModel>.Success(result);
    }

    public async Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target)
    {
        var renew = await RenewalRepo.Value.LatestOf(target);

        return ApiResult<PlanInfoModel>.Success(new PlanInfoModel
        {
            Target = target,
            SimultaneousUserCount = renew?.SimultaneousUse,
            Months = renew?.MonthLimit,
            Gigabytes = renew?.GetTrafficLimitInGig(),
        });
    }

    public ApiResult<int> Estimate(int users, int months, int gigabytes)
    {
        var result = PriceCalc.Value.CalculatePrice(users, months, gigabytes);
        return ApiResult<int>.Success(result);
    }

    public Task<ApiResult> TemporaryRenewal(string target, int months, int gigabytes)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResult<RenewalResult>> Renewal(string target, int count, int months, int gigabytes)
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
        var fetch_state = SessionRepo.Value.GetPlanState(account.PermanentUserId);
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

            var nas_list = new List<NasEntity>();
            if (state.RestrictedServerIP != null)
            {
                var nas = await NasRepo.Value.GetNasInfo(state.RestrictedServerIP);
                if (nas != null) nas_list.Add(nas);
            }

            if (nas_list.Count < 0)
            {
                nas_list.AddRange(await NasRepo.Value.GetAll());
            }

            _ = VpnNodeSrv.Value.CloseConnections(nas_list, user.Username, state.SimultaneousUserCount.Value - count);
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
            var realm = await ServerMngSrv.Value.GetAvailableRealm(user.CloudId);
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

        account.WarningTimes = null;
        account.Balance -= estimate;
        switch (type)
        {
            case PlanType.Monthly:
                value = DateTime.Now.AddMonthToDays(value);
                break;
            case PlanType.Traffic:
                value *= 25;
                break;
        }

        var tranAccount = AccountRepo.Value.BeginTransaction();

        try
        {
            await AccountRepo.Value.Save(account);

            var success = await RadiusSrv.Value.InsertTopUpAndMakeActive(user.Id, type, value);

            if (!success)
            {
                throw new Exception("Insert top-up and make active was unsuccessful!");
            }

            var checkOnRenewal = IPlanApplication.OnRenewalDelegation(new RenewalEvent());

            if (!checkOnRenewal)
            {
                throw new Exception("On renewal delegation was unsuccessful!");
            }

            tranAccount.Commit();
        }
        catch
        {
            _ = RadiusSrv.Value.ActivePermanentUser(user.Id, false);

            tranAccount.Rollback();

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
