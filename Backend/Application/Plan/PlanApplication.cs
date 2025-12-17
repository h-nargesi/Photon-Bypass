using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    Lazy<IRenewalRepository> renewal_repo,
    Lazy<IPlanStateRepository> plan_repo,
    Lazy<IPriceCalculator> price_calc,
    Lazy<IAccountRepository> account_repo,
    Lazy<ISessionRadiusSyncService> session_radius_srv,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IServerManagementService> server_mng_srv,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IJobContext> job_context)
    : IPlanApplication
{
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<IPlanStateRepository> PlanRepo { get; } = plan_repo;
    private Lazy<IPriceCalculator> PriceCalc { get; } = price_calc;
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<ISessionRadiusSyncService> SessionRadiusSrv { get; } = session_radius_srv;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IServerManagementService> ServerMngSrv { get; } = server_mng_srv;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult<UserPlanInfoModel>> GetPlanState(string target)
    {
        var account_id = await AccountRepo.Value.GetActiveAccountId(target);
        if (!account_id.HasValue)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var state = (await PlanRepo.Value.GetPlanState(account_id.Value)) ??
                    throw new UserException("هیچ پلنی برای این کاربر فعال نیست!",
                        $"The plan-state not found for target={target}, account-id={account_id.Value}");

        Log.Information(
            "[user: {0}] session state: (target:{1}, user-count:{2}, data-left:{3}, total-data:{4}, time-left:{5}-{6})",
            JobContext.Value.Username, target, state.SimultaneousUser,
            state.GetTrafficLeftInGig(), state.GetTrafficLimitInGig(), state.TimeLeft?.TotalDays,
            state.TimeLeft?.Hours);

        _ = ServerMngSrv.Value.UpdateTrafficData();

        return ApiResult<UserPlanInfoModel>.Success(new UserPlanInfoModel
        {
            RemainsTitle = state.GetRemainsTitle(),
            SimultaneousUserCount = state.SimultaneousUser,
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

        var renew = await PlanRepo.Value.GetPlanState(account_id.Value);

        _ = ServerMngSrv.Value.UpdateTrafficData();

        return ApiResult<PlanInfoModel>.Success(new PlanInfoModel
        {
            Target = target,
            SimultaneousUserCount = renew?.SimultaneousUser,
            Days = renew?.TimeLimitInDays,
            Gigabytes = renew?.GetTrafficLimitInGig(),
        });
    }

    public async Task<ApiResult<int>> Estimate(string target, byte users, short days, int gigabytes)
    {
        var account = (await AccountRepo.Value.GetAccount(target)) ??
                      throw new UserException("کاربر مورد نظر پیدا نشد!");

        var result = PriceCalc.Value.CalculatePrice(account.CalculationMethod ?? 0, users, days, gigabytes);
        return ApiResult<int>.Success(result);
    }

    public async Task<ApiResult<RenewalResult>> Renewal(string target, byte count, short days, int gigabytes)
    {
        var account = (await AccountRepo.Value.GetAccount(target)) ??
                      throw new UserException("کاربر مورد نظر پیدا نشد!");

        if (!account.IsActive)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        var estimate = PriceCalc.Value.CalculatePrice(account.CalculationMethod ?? 0, count, days, gigabytes);

        if (account.CheckMoneyNeed(estimate, out var money_need))
        {
            Log.Information(@"
[user: {0}] Plan renewal request:
    account=(user:{7}, balance:{6})
    request=(taget:{1}, user-count:{2}, days={3}, traffic:{4}, estimate:{5})
",
                JobContext.Value.Username, target, count, days, gigabytes, account.Balance, estimate,
                JobContext.Value.Username);

            return ApiResult<RenewalResult>.Success(new RenewalResult
            {
                CurrentPrice = account.Balance,
                MoneyNeeds = money_need,
            });
        }

        var current_state = (await PlanRepo.Value.GetPlanState(account.Id)) ??
                            throw new Exception($"Plan state not found for target: {target}");

        Log.Information(@"
[user: {0}] Plan current state:
    account=(user:{11}, balance:{9})
    request=(taget:{1}, count:{2}, days:{3}, gigabytes:{4}, estimate:{10})
    current=(count:{5}, left-days:{6}, left-hours:{7}, left-gigabytes:{8})
",
            JobContext.Value.Username,
            target, count, days, gigabytes,
            current_state.SimultaneousUser, current_state.TimeLeft?.TotalDays, current_state.TimeLeft?.Hours,
            current_state.GetTrafficLeftInGig(),
            account.Balance, estimate,
            JobContext.Value.Username);

        var renew = new RenewalEntity
        {
            AccountId = account.Id,
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            RateLimitInMeg = null,
            SimultaneousUser = count,
            RestrictedRealmId = await RenewalRepo.Value.GetTopRestrictedRealmId(account.Id),
        };

        if (renew.RestrictedRealmId == null || current_state.LastConnectTime == null ||
            current_state.LastConnectTime.Value < DateTime.Now.AddDays(-7))
        {
            renew.RestrictedRealmId = (await ServerMngSrv.Value.GetAvailableRealm()).Id;
        }

        if (renew.RenewalValidation(out var user_exception))
        {
            Log.Information(@"[user: {0}] Plan current state:
    request=(taget:{1}, count:{2}, days:{3}, gigabytes:{4})
    message={5}",
                JobContext.Value.Username, target, count, days, gigabytes, user_exception.Message);

            throw user_exception;
        }

        var transaction = await AccountRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            account.Balance -= estimate;

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.Id,
                Category = EventCategory.Transaction,
                Type = EventType.Information,
                Title = "مالی",
                Description = "از حساب کم شد.",
                Price = -estimate,
            });

            await AccountRepo.Value.Save(account);

            await RenewalRepo.Value.Save(renew);

            var check_on_renewal = IPlanApplication.OnRenewalDelegation(new RenewalEvent());

            if (!check_on_renewal)
            {
                throw new Exception($"On renewal delegation was unsuccessful! (target={target})");
            }

            await AccountRadiusSrv.Value.SyncUserAndActive(account, renew);

            transaction.Commit();
        }
        catch
        {
            _ = AccountRadiusSrv.Value.DeactivateUsers([account.Username]);

            transaction.Rollback();

            _ = HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.Id,
                Category = EventCategory.Renewal,
                Type = EventType.Error,
                Title = "تمدید",
                Description = "خطا در تمدید پلن!",
            });

            throw;
        }

        if (count < current_state.SimultaneousUser)
        {
            Log.Information("""
                            [user: {0}] Plan renewal change user count: (closing connections)
                                change=(taget:{1}, user-count:{2}, to:{3})
                            """,
                JobContext.Value.Username, target, current_state.SimultaneousUser, count);

            _ = SessionRadiusSrv.Value.CloseConnectionByUsername(renew.RestrictedRealmId, account.Username);
        }

        _ = HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.Id,
            Category = EventCategory.Renewal,
            Type = EventType.Success,
            Title = "تمدید",
            Description = "پلن تمید شد.",
            Value = renew.GetPlanTitle(),
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