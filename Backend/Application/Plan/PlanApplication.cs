using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Billing.Model;
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
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    Lazy<IRenewalRepository> renewal_repo,
    Lazy<IPlanStateRepository> plan_repo,
    IPriceCalculator price_calc,
    Lazy<IWalletRepository> wallet_repo,
    Lazy<IAccountRepository> account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<ISessionRadiusSyncService> session_radius_srv,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IServerManagementService> server_mng_srv,
    Lazy<IBillingApplication> billing_app,
    Lazy<IJobContext> job_context)
    : IPlanApplication
{
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<IPlanStateRepository> PlanRepo { get; } = plan_repo;
    private IPriceCalculator PriceCalc { get; } = price_calc;
    private Lazy<IWalletRepository> WalletRepo { get; } = wallet_repo;
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<ISessionRadiusSyncService> SessionRadiusSrv { get; } = session_radius_srv;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IServerManagementService> ServerMngSrv { get; } = server_mng_srv;
    private Lazy<IBillingApplication> BillingApp { get; } = billing_app;
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

    public async Task<ApiResult<EstimateResult>> Estimate(string target, byte users, short days, int gigabytes)
    {
        var account = (await AccountRepo.Value.GetAccount(target)) ??
                      throw new UserException("کاربر مورد نظر پیدا نشد!");

        var renew = new RenewalEntity
        {
            AccountId = account.Id,
            SimultaneousUser = users,
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
        };

        if (renew.RenewalValidation(account, out var user_exception))
        {
            throw user_exception;
        }

        var result = PriceCalc.CalculatePrice(account.CalculationMethod ?? 0, users, days, gigabytes);

        return ApiResult<EstimateResult>.Success(new EstimateResult
        {
            Days = renew.TimeLimitInDays,
            Gigabytes = renew.TrafficLimit / StaticValues.BytesInGigLong,
            SimultaneousUserCount = renew.SimultaneousUser,
            Price = result,
        });
    }

    public async Task<ApiResult<RenewalResult>> Renewal(string target, byte count, short days, int gigabytes)
    {
        var account = (await AccountRepo.Value.GetAccount(target)) ??
                      throw new UserException("کاربر مورد نظر پیدا نشد!");

        JobContext.Value.InjectJobContext(account.Id);

        return await Renewal(account, null, count, days, gigabytes);
    }

    public async Task<ApiResult<RenewalResult>> Renewal(int account_id, int payment_id, string action)
    {
        string? target_name = null; byte? count = null; short? days = null; int? gigabytes = null;
        action.Split('|')
            .Where(x => !string.IsNullOrEmpty(x))
            .Foreach(x =>
            {
                switch (x.Last())
                {
                    case 't': target_name = x[..^1]; break;
                    case 'u': count = byte.Parse(x[..^1]); break;
                    case 'd': days = short.Parse(x[..^1]); break;
                    case 'g': gigabytes = int.Parse(x[..^1]); break;
                }
            });

        if (target_name == null || count == null || days == null || gigabytes == null)
        {
            throw new Exception($"Invalid target={target_name} action={action} [count={count}, days={days}, gigabytes={gigabytes}]");
        }

        var account = await AccountRepo.Value.GetUsernamesByAccountId([account_id]);

        if (account.Count != 1)
        {
            throw new UserException("کاربر مورد نظر پیدا نشد!");
        }

        var target = (await AccountRepo.Value.GetAccount(target_name)) ??
                      throw new UserException("کاربر مورد نظر پیدا نشد!");

        JobContext.Value.InjectJobContext(account_id, account[account_id], target_name);

        return await Renewal(target, payment_id, count.Value, days.Value, gigabytes.Value);
    }

    public async Task<ApiResult<RenewalResult>> Renewal(AccountEntity account, int? payment_id, byte count, short days, int gigabytes)
    {
        if (!account.IsActive)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        var estimate = PriceCalc.CalculatePrice(account.CalculationMethod ?? 0, count, days, gigabytes);
        var balance = await WalletRepo.Value.GetBalance(account.Id);

        if (account.CheckMoneyNeed(balance, estimate, out var money_need))
        {
            Log.Information(@"
[user: {0}] Plan renewal request:
    account=(user:{7}, balance:{6})
    request=(taget:{1}, user-count:{2}, days={3}, traffic:{4}, estimate:{5})
",
                JobContext.Value.Username, account.Username, count, days, gigabytes, balance, estimate,
                JobContext.Value.Username);

            var invoice_info = await BillingApp.Value.GenerateInvoiceCode(new NewInvoiceInfo
            {
                Price = money_need,
                Action = $"{account.Username}t|{count}u|{days}d|{gigabytes}g",
            });

            if (invoice_info.Code / 100 != 2)
            {
                return new ApiResult<RenewalResult>
                {
                    Code = invoice_info.Code,
                    Message = invoice_info.Message,
                    MessageMethod = invoice_info.MessageMethod,
                    Developer = invoice_info.Developer,
                };
            }

            return ApiResult<RenewalResult>.Success(new RenewalResult
            {
                CurrentPrice = balance,
                InvocieCode = invoice_info.Data,
            });
        }

        var current_state = (await PlanRepo.Value.GetPlanState(account.Id)) ??
                            throw new Exception($"Plan state not found for target: {account.Username}");

        Log.Information(@"
[user: {0}] Plan current state:
    account=(user:{11}, balance:{9})
    request=(taget:{1}, count:{2}, days:{3}, gigabytes:{4}, estimate:{10})
    current=(count:{5}, left-days:{6}, left-hours:{7}, left-gigabytes:{8})
",
            JobContext.Value.Username,
            account.Username, count, days, gigabytes,
            current_state.SimultaneousUser, current_state.TimeLeft?.TotalDays, current_state.TimeLeft?.Hours,
            current_state.GetTrafficLeftInGig(),
            balance, estimate,
            JobContext.Value.Username);

        var renew = new RenewalEntity
        {
            AccountId = account.Id,
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            RateLimitInMeg = null,
            SimultaneousUser = count,
            WalletCredit = payment_id,
            RestrictedRealmId = await RenewalRepo.Value.GetTopRestrictedRealmId(account.Id),
        };

        if (renew.RestrictedRealmId == null || current_state.LastConnectTime == null ||
            current_state.LastConnectTime.Value < DateTime.Now.AddDays(-7))
        {
            renew.RestrictedRealmId = (await ServerMngSrv.Value.GetAvailableRealm()).Id;
        }

        if (renew.RenewalValidation(account, out var user_exception))
        {
            Log.Information(@"[user: {0}] Plan current state:
    request=(taget:{1}, count:{2}, days:{3}, gigabytes:{4})
    message={5}",
                JobContext.Value.Username, account.Username, count, days, gigabytes, user_exception.Message);

            throw user_exception;
        }

        await AccountRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            var wallet_debit = new WalletEntity
            {
                AccountId = account.Id,
                Amount = estimate,
                Direction = BalanceDirection.Debit,
                Status = BalanceStatus.Completed,
                Description = renew.GetPlanTitle(),
            };
            await WalletRepo.Value.Save(wallet_debit);

            balance -= estimate;

            renew.WalletDebit = wallet_debit.Id;

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.Id,
                Category = EventCategory.Transaction,
                Type = EventType.Information,
                Title = "مالی",
                Description = "از حساب کم شد.",
                Price = -estimate,
            });

            await RenewalRepo.Value.Save(renew);

            var check_on_renewal = IPlanApplication.OnRenewalDelegation(new RenewalEvent());

            if (!check_on_renewal)
            {
                throw new Exception($"On renewal delegation was unsuccessful! (target={account.Username})");
            }

            await AccountRadiusSrv.Value.SyncUserAndActive(account, renew);

            await AccountRepo.Value.DbContext.CommitAsync();
        }
        catch
        {
            await AccountRadiusSrv.Value.DeactivateUsers([account.Username]);

            await AccountRepo.Value.DbContext.RollbackAsync();

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
                JobContext.Value.Username, account.Username, current_state.SimultaneousUser, count);

            await SessionRadiusSrv.Value.CloseConnectionByUsername(renew.RestrictedRealmId, account.Username);
        }

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
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
            JobContext.Value.Username, account.Username, count, days, gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = balance,
        });
    }

}