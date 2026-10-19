using PhotonBypass.Application.Account;
using PhotonBypass.Application.Billing;
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
    Lazy<IPlanUsageReader> plan_usage_reader,
    IPriceCalculator price_calc,
    Lazy<IWalletRepository> wallet_repo,
    Lazy<IAccountRepository> account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<ISessionRadiusSyncService> session_radius_srv,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IServerManagementService> server_mng_srv,
    Lazy<IAccountApplication> account_app,
    Lazy<IBillingApplication> billing_app,
    Lazy<ISelfServiceGuard> self_service_guard,
    Lazy<IJobContext> job_context)
    : IPlanApplication
{
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<IPlanStateRepository> PlanRepo { get; } = plan_repo;
    private Lazy<IPlanUsageReader> PlanUsageReader { get; } = plan_usage_reader;
    private IPriceCalculator PriceCalc { get; } = price_calc;
    private Lazy<IWalletRepository> WalletRepo { get; } = wallet_repo;
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<ISessionRadiusSyncService> SessionRadiusSrv { get; } = session_radius_srv;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IServerManagementService> ServerMngSrv { get; } = server_mng_srv;
    private Lazy<IBillingApplication> BillingApp { get; } = billing_app;
    private Lazy<IAccountApplication> AccountApp { get; } = account_app;
    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult<PlanStateModel>> GetPlanState(string target)
    {
        var account_id = await AccountRepo.Value.GetActiveAccountId(target);
        if (!account_id.HasValue)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var state = await PlanRepo.Value.GetPlanState(account_id.Value);

        Log.Information(
            "[user: {0}] session state: (target:{1}, user-count:{2}, data-left:{3}, total-data:{4}, time-left:{5}-{6})",
            JobContext.Value.Username, target, state?.SimultaneousUser,
            state?.GetTrafficLeftInGig(), state?.GetTrafficLimitInGig(), state?.TimeLeft?.TotalDays,
            state?.TimeLeft?.Hours);

        return ApiResult<PlanStateModel>.Success(new PlanStateModel
        {
            RemainsTitle = state?.GetRemainsTitle(),
            SimultaneousUserCount = state?.SimultaneousUser,
            RemainsTimePercent = (int?)state?.TimeLeftPercent,
            RemainsTrafficPercent = (int?)state?.TrafficLeftPercent,
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
        var data_plane_info = renew == null
            ? null
            : await ServerMngSrv.Value.GetDataPlaneInfo(renew.RestrictedRealmId);

        return ApiResult<PlanInfoModel>.Success(new PlanInfoModel
        {
            Target = target,
            SimultaneousUserCount = renew?.SimultaneousUser,
            Days = renew?.TimeLimitInDays,
            Gigabytes = renew?.GetTrafficLimitInGig(),
            Kind = data_plane_info?.Kind,
            SupportsOvpnCert = data_plane_info?.SupportsOvpnCert,
            SupportsPasswordChange = data_plane_info?.SupportsPasswordChange,
            SupportsCredentialRotation = data_plane_info?.SupportsCredentialRotation,
        });
    }

    public async Task<ApiResult<EstimateResult>> Estimate(string target, byte users, short days, int gigabytes)
    {
        await SelfServiceGuard.Value.EnsureSelfService();

        var account = await AccountApp.Value.GetActiveUser(target);

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

        var result = await PriceCalc.CalculatePrice(account.CalculationMethod ?? 0, users,
            renew.TimeLimitInDays ?? days, gigabytes);

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
        // D4: the invoice and the wallet debit always belong to the paying
        // actor; the renewal itself is applied to the target account.
        var payer = await SelfServiceGuard.Value.ResolvePayingActor();

        var account = await AccountApp.Value.GetActiveUser(target);

        JobContext.Value.InjectJobContext(payer.Id, payer.Username, account.Username);

        var renewal = new RenewalEntity
        {
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            SimultaneousUser = count,
        };

        if (renewal.RenewalValidation(account, out var validation_exception))
        {
            Log.Information(@"[user: {0}] Plan renewal request rejected before invoice:
    request=(taget:{1}, count:{2}, days={3}, gigabytes:{4})
    message={5}",
                JobContext.Value.Username, account.Username, count, days, gigabytes, validation_exception.Message);

            throw validation_exception;
        }

        var estimate = await PriceCalc.CalculatePrice(account.CalculationMethod ?? 0, count,
            renewal.TimeLimitInDays ?? days, gigabytes);
        var balance = await WalletRepo.Value.GetBalance(payer.Id);

        Log.Information(@"
[user: {0}] Plan renewal request:
    account=(user:{6}, balance:{5})
    request=(taget:{1}, user-count:{2}, days={3}, traffic:{4}, estimate:{7})
",
            JobContext.Value.Username, account.Username, count, days, gigabytes, balance,
            payer.Username, estimate);

        var invoice_code = await BillingApp.Value.IssuePlanInvoice(estimate,
            $"{account.Username}t|{count}u|{days}d|{gigabytes}g", renewal.GetPlanTitle());

        if (invoice_code.Code / 100 != 2)
        {
            return new ApiResult<RenewalResult>
            {
                Code = invoice_code.Code,
                Message = invoice_code.Message,
                MessageMethod = invoice_code.MessageMethod,
                Developer = invoice_code.Developer,
            };
        }

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = balance,
            InvoiceCode = invoice_code.Data,
        });
    }

    public async Task<ApiResult<RenewalResult>> Renewal(int account_id, int? payment_id, string action, int? invoice_code = null)
    {
        string? target_name = null; byte? count = null; short? days = null; int? gigabytes = null;
        action.Split('|')
            .Where(x => !string.IsNullOrEmpty(x))
            .Foreach(x =>
            {
                switch (x.Last())
                {
                    case 't': target_name = x[..^1]; break;
                    case 'u':
                        if (!byte.TryParse(x[..^1], out var parsed_count))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action user-count: {x}");
                        }
                        count = parsed_count;
                        break;
                    case 'd':
                        if (!short.TryParse(x[..^1], out var parsed_days))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action days: {x}");
                        }
                        days = parsed_days;
                        break;
                    case 'g':
                        if (!int.TryParse(x[..^1], out var parsed_gigabytes))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action gigabytes: {x}");
                        }
                        gigabytes = parsed_gigabytes;
                        break;
                }
            });

        if (target_name == null || count == null || days == null || gigabytes == null)
        {
            throw new Exception($"Invalid target={target_name} action={action} [count={count}, days={days}, gigabytes={gigabytes}]");
        }

        var payer = await AccountRepo.Value.GetActiveAccount(account_id)
                    ?? throw new UserException("کاربر مورد نظر پیدا نشد!",
                        $"Payer account not found or inactive: id={account_id}");

        var target = await AccountApp.Value.GetActiveUser(target_name);

        JobContext.Value.InjectJobContext(payer.Id, payer.Username, target_name);

        return await Renewal(target, payer, payment_id, count.Value, days.Value, gigabytes.Value, invoice_code);
    }

    private async Task<ApiResult<RenewalResult>> Renewal(AccountEntity account, AccountEntity payer, int? payment_id, byte count, short days, int gigabytes, int? invoice_code)
    {
        if (!account.IsActive)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        var current_state = (await PlanRepo.Value.GetPlanState(account.Id)) ??
                            throw new Exception($"Plan state not found for target: {account.Username}");

        var renew = new RenewalEntity
        {
            AccountId = account.Id,
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            RateLimitInMeg = null,
            SimultaneousUser = count,
            WalletCredit = payment_id,
            RestrictedRealmId = await PlanRepo.Value.GetTopRestrictedRealmId(account.Id),
        };

        var current_usage = await PlanUsageReader.Value.GetUsage(account.Id);
        var last_activity = current_usage?.LastActiveTime ?? current_state.LastConnectTime;

        if (renew.RestrictedRealmId == null || last_activity == null ||
            last_activity < DateTime.Now.AddDays(-7))
        {
            renew.RestrictedRealmId = (await ServerMngSrv.Value.GetAvailableRealm()).Id;
        }

        if (renew.RenewalValidation(account, out var user_exception))
        {
            Log.Information(@"[user: {0}] Plan current state:
    request=(taget:{1}, count:{2}, days={3}, gigabytes:{4})
    message={5}",
                JobContext.Value.Username, account.Username, count, days, gigabytes, user_exception.Message);

            throw user_exception;
        }

        // D16 (docs/17): stacked plan state — both values are computed once,
        // right here, and stored on the renewal; the x-ui push and the usage
        // reader only read them. Expiry stacks on the still-running plan, the
        // traffic quota on the cumulative remaining (view TrafficLeft of the
        // current band); NULL means "no limit" on either axis.
        renew.ExpirationDate = renew.TimeLimitInDays.HasValue
            ? LaterOf(DateTime.Now, current_state.ExpirationDate).AddDays(renew.TimeLimitInDays.Value)
            : null;
        renew.CumulativeTrafficLeft = StackRemainingTraffic(current_state, renew.TrafficLimit);

        var estimate = await PriceCalc.CalculatePrice(account.CalculationMethod ?? 0, count,
            renew.TimeLimitInDays ?? days, gigabytes);
        var balance = await WalletRepo.Value.GetBalance(payer.Id);

        if (payer.CheckMoneyNeed(balance, estimate, out _))
        {
            throw new UserException("موجودی کیف پول برای اجرای این تمدید کافی نیست!",
                $"Settlement renewal with money-need: payer={payer.Username}, target={account.Username}, balance={balance}, estimate={estimate}");
        }

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
            payer.Username);

        await AccountRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            var wallet_debit = new WalletEntity
            {
                AccountId = payer.Id,
                Amount = estimate,
                Direction = BalanceDirection.Debit,
                Status = BalanceStatus.Completed,
                InvoiceCode = invoice_code,
                Description = renew.GetPlanTitle(),
            };
            await WalletRepo.Value.Save(wallet_debit);

            balance -= estimate;

            renew.WalletDebit = wallet_debit.Id;

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = payer.Id,
                Category = EventCategory.Transaction,
                Type = EventType.Information,
                Title = "مالی",
                Description = "از حساب کم شد.",
                Price = -estimate,
            });

            await RenewalRepo.Value.Save(renew);

            await AccountRadiusSrv.Value.SyncUserAndActive(account, renew);

            await AccountRepo.Value.DbContext.CommitAsync();
        }
        catch
        {
            try
            {
                await AccountRepo.Value.DbContext.RollbackAsync();
            }
            catch (Exception rollback_error)
            {
                Log.Error(rollback_error, "Renewal rollback failed: target={0}", account.Username);
            }

            try
            {
                await AccountRadiusSrv.Value.DeactivateUsers([account.Username]);
            }
            catch (Exception compensation_error)
            {
                Log.Error(compensation_error,
                    "RADIUS-COMPENSATION-FAILED: radius deactivation after renewal rollback failed: target={0}",
                    account.Username);
            }

            try
            {
                await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
                {
                    Target = account.Id,
                    Category = EventCategory.Renewal,
                    Type = EventType.Error,
                    Title = "تمدید",
                    Description = "خطا در تمدید پلن!",
                });
            }
            catch (Exception history_error)
            {
                Log.Error(history_error, "Saving renewal failure history failed: target={0}", account.Username);
            }

            throw;
        }

        // The wallet debit, the renewal and the radius sync are committed at
        // this point: everything below is best-effort. A failure must not
        // propagate — the settlement callers (SettleWalletCore /
        // RunRenewalAfterReceipt) would revert the invoice to pending or tell
        // the user the payment failed although the plan is already active and
        // the money is gone.
        try
        {
            if (count < current_state.SimultaneousUser)
            {
                Log.Information("""
                                [user: {0}] Plan renewal change user count: (closing connections)
                                    change=(taget:{1}, user-count:{2}, to:{3})
                                """,
                    JobContext.Value.Username, account.Username, current_state.SimultaneousUser, count);

                await SessionRadiusSrv.Value.CloseConnectionByUsername(renew.RestrictedRealmId, account.Username);
            }
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Closing connections after renewal commit failed: target={0}", account.Username);
        }

        try
        {
            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.Id,
                Category = EventCategory.Renewal,
                Type = EventType.Success,
                Title = "تمدید",
                Description = "پلن تمید شد.",
                Value = renew.GetPlanTitle(),
            });
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Saving renewal success history failed: target={0}", account.Username);
        }

        try
        {
            await ServerMngSrv.Value.CheckUserServerBalance();
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Server capacity check after renewal commit failed: target={0}", account.Username);
        }

        Log.Information("[user: {0}] Plan renewal finished: ({1}, {2}, {3}, {4})",
            JobContext.Value.Username, account.Username, count, days, gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = balance,
        });
    }

    private static DateTime LaterOf(DateTime now, DateTime? current_expiry)
    {
        return current_expiry.HasValue && current_expiry.Value > now ? current_expiry.Value : now;
    }

    /// <summary>
    /// D16 (docs/17): the traffic quota carried into the new renewal — the
    /// cumulative remaining of the current band (view TrafficLeft; a NULL on a
    /// limited band means "no usage recorded yet", i.e. the full quota) plus
    /// this renewal's share, clamped at zero for overdrawn plans. A NULL
    /// previous remaining means an unlimited current plan, so the stack is
    /// unlimited; an account with no plan at all starts from its own share.
    /// </summary>
    private static long? StackRemainingTraffic(PlanStateEntity current_state, long? new_share)
    {
        var has_current_plan = current_state.TrafficLimit.HasValue || current_state.TimeLimitInDays.HasValue;

        if (!has_current_plan)
        {
            return new_share;
        }

        var remaining = current_state.TrafficLeft ?? current_state.TrafficLimit;

        return remaining.HasValue ? Math.Max(0, remaining.Value) + new_share : null;
    }
}