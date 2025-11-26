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
        var account_id = await AccountRepo.Value.GetActiveAccountId(target);
        if (!account_id.HasValue)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var state = (await PlanRepo.Value.GetPlanState(account_id.Value)) ??
                    throw new Exception($"The plan-state not found for target={target}, account-id={account_id.Value}");

        Log.Information("[user: {0}] session state: (target:{1}, user-count:{2}, data-left:{3}, total-data:{4}, time-left:{5}-{6})",
            JobContext.Value.Username, target, state.SimultaneousUserCount,
            state.GetTrafficLeftInGig(), state.GetTrafficLimitInGig(), state.TimeLeft?.TotalDays, state.TimeLeft?.Hours);

        return ApiResult<UserPlanInfoModel>.Success(new UserPlanInfoModel
        {
            RemainsTitle = state.GetRemainsTitle(),
            SimultaneousUserCount = state.SimultaneousUserCount,
            RemainsTimePercent = (int?)state.TimeLeftPercent,
            RemainsTrafficPercent = (int?)state.TrafficLeftPercent,
        });
    }

    public async Task<ApiResult<PlanInfoModel>> GetPlanInfo(string target)
    {
        var account_id = await AccountRepo.Value.GetActiveAccountId(target);
        if (!account_id.HasValue)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var renew = await RenewalRepo.LatestOf(account_id.Value);

        return ApiResult<PlanInfoModel>.Success(new PlanInfoModel
        {
            Target = target,
            SimultaneousUserCount = renew?.SimultaneousUse,
            Days = renew?.TimeLimitInDays,
            Gigabytes = renew?.GetTrafficLimitInGig(),
        });
    }

    public ApiResult<int> Estimate(int users, int days, int gigabytes)
    {
        var result = PriceCalc.Value.CalculatePrice(users, days, gigabytes);
        return ApiResult<int>.Success(result);
    }

    public Task<ApiResult> TemporaryRenewal(string target, int days, int gigabytes)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResult<RenewalResult>> Renewal(string target, int count, int days, int gigabytes)
    {
        var account = (await AccountRepo.Value.GetAccount(target)) ??
            throw new UserException("کاربر مورد نظر پیدا نشد!");

        if (!account.Active)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        var estimate = PriceCalc.Value.CalculatePrice(count, days, gigabytes);

        if (account.CheckMoneyNeed(estimate, out var money_need))
        {
            Log.Information(@"[user: {0}] Plan renewal request:
    account=(user:{0}, balance:{6})
    request=(taget:{1}, user-count:{2}, days={3}, traffic:{4}, estimate:{7})",
                JobContext.Value.Username, target, count, days, gigabytes, account.Balance, estimate);

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
    request=(taget:{1}, count:{2}, days:{3}, gigabytes:{4}, estimate:{10})
    current=(count:{5}, left-days:{6}, left-hours:{7}, left-gigabytes:{8})",
            JobContext.Value.Username,
            target, count, days, gigabytes,
            state.SimultaneousUserCount, state.TimeLeft?.TotalDays, state.TimeLeft?.Hours, state.GetTrafficLeftInGig(),
            account.Balance, estimate);

        var renew = new RenewalEntity
        {
            AccountId = account.Id,
            TimeLimitInDays = days,
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
    request=(taget:{1}, count:{2}, days:{3}, gigabytes:{4})
    message={5}",
                JobContext.Value.Username, target, count, days, gigabytes, user_message);

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
            JobContext.Value.Username, target, count, days, gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = account.Balance,
            MoneyNeeds = 0,
        });
    }

}
