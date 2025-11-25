using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    IRenewalRepository RenewalRepo,
    Lazy<IPlanStateRepository> PlanRepo,
    Lazy<IPriceCalculator> PriceCalc,
    Lazy<IAccountRepository> AccountRepo,
    Lazy<ISessionRadiusSyncService> SessionRadiusSrv,
    Lazy<IAccountRadiusSyncService> AccountRadiusSrv,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<INasRepository> NasRepo,
    Lazy<IJobContext> JobContext)
    : IPlanApplication
{
    public async Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target)
    {
        if (await AccountRepo.Value.IsInactive(target))
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var renew = await RenewalRepo.LatestOf(target);

        if (renew == null)
        {
            return new ApiResult<UserPlanInfoModel>
            {
                Message = "بدون پلن",
            };
        }

        var state = (await PlanRepo.Value.GetPlanState(renew.AccountId)) ??
                    throw new Exception($"The plan-state not found for target={target}, account-id={renew.AccountId}, realm-id={renew.RestrictedRealmId}");

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
            if (state is { TimeLeft: not null, ExpirationDate: not null })
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
        if (await AccountRepo.Value.IsInactive(target))
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var renew = await RenewalRepo.LatestOf(target);

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

        if (!account.Active)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        var estimate = PriceCalc.Value.CalculatePrice(count, months, gigabytes);

        if (account.CheckMoneyNeed(estimate, out var money_need))
        {
            Log.Information(@"[user: {0}] Plan renewal request:
    account=(user:{0}, balance:{6})
    request=(taget:{1}, user-count:{2}, months={3}, traffic:{4}, estimate:{7})",
                JobContext.Value.Username, target, count, months, gigabytes, account.Balance, estimate);

            return ApiResult<RenewalResult>.Success(new RenewalResult
            {
                CurrentPrice = account.Balance,
                MoneyNeeds = money_need,
            });
        }

        var state = (await PlanRepo.Value.GetPlanState(account.Id)) ??
            throw new Exception($"Plan state not found for target: {target}");

        Log.Information(@"[user: {0}] Plan current state:
    account=(user:{0}, balance:{9})
    request=(taget:{1}, count:{2}, months:{3}, gigabytes:{4}, estimate:{10})
    current=(count:{5}, left-days:{6}, left-hours:{7}, left-gigabytes:{8})",
            JobContext.Value.Username,
            target, count, months, gigabytes,
            state.SimultaneousUserCount, state.TimeLeft?.TotalDays, state.TimeLeft?.Hours, state.GetTrafficLeftInGig(),
            account.Balance, estimate);

        var renew = new RenewalEntity
        {
            AccountId = account.Id,
            MonthLimit = months,
            TrafficLimit = (int)(gigabytes * StaticValues.BytesInGig),
            SimultaneousUse = count,
        };

        var previous_plan = await RenewalRepo.LatestOf(account.Id);
        renew.RestrictedRealmId = previous_plan?.RestrictedRealmId;

        if (renew.RestrictedRealmId == null || state.LastConnectTime == null || state.LastConnectTime.Value < DateTime.Now.AddDays(-7))
        {
            renew.RestrictedRealmId = (await ServerMngSrv.Value.GetAvailableRealm()).Id;
        }

        if (renew.RenewalValidation(out var user_message))
        {
            Log.Information(@"[user: {0}] Plan current state:
    request=(taget:{1}, count:{2}, months:{3}, gigabytes:{4})
    message={5}",
                JobContext.Value.Username, target, count, months, gigabytes, user_message);

            throw new UserException(user_message);
        }

        var syncronization = AccountRadiusSrv.Value.SyncUserAndActive(account);

        if (count < state.SimultaneousUserCount)
        {
            Log.Information(@"[user: {0}] Plan renewal change user count: (closing connections)
    change=(taget:{1}, user-count:{2}, to:{3})",
                JobContext.Value.Username, target, state.SimultaneousUserCount, count);

            var nas_list = new List<NasEntity>();
            if (previous_plan?.RestrictedRealmId != null)
            {
                nas_list.AddRange(await NasRepo.Value.GetAllActiveInRealm(previous_plan.RestrictedRealmId.Value));
            }

            if (nas_list.Count < 0)
            {
                nas_list.AddRange(await NasRepo.Value.GetAllActive());
            }

            _ = SessionRadiusSrv.Value.CloseConnections(nas_list, account.Username, state.SimultaneousUserCount.Value - count);
        }

        await syncronization;

        var tranAccount = await AccountRepo.Value.BeginTransactionAsync();

        try
        {
            account.WarningTimes = null;
            account.Balance -= estimate;

            await AccountRepo.Value.Save(account);

            await RenewalRepo.Save(renew);

            var checkOnRenewal = IPlanApplication.OnRenewalDelegation(new RenewalEvent());

            if (!checkOnRenewal)
            {
                throw new Exception("On renewal delegation was unsuccessful!");
            }

            tranAccount.Commit();
        }
        catch
        {
            _ = AccountRadiusSrv.Value.DeactivateUser([account.Username]);

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
            Value = renew.GetTitle(),
        });

        _ = ServerMngSrv.Value.CheckUserServerBalance();

        Log.Information("[user: {0}] Plan renewal finished: ({1}, {2}, {3}, {4})",
            JobContext.Value.Username, target, count, months, gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = account.Balance,
            MoneyNeeds = 0,
        });
    }

}
