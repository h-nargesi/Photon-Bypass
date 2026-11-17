using Microsoft.Extensions.Options;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
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
    Lazy<IPanelUserRepository> panel_user_repo,
    Lazy<IVpnAccountRepository> vpn_account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<ISessionRadiusSyncService> session_radius_srv,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IServerManagementService> server_mng_srv,
    Lazy<IBillingApplication> billing_app,
    Lazy<ISelfServiceGuard> self_service_guard,
    Lazy<IJobContext> job_context,
    Lazy<IEmailService> email_srv,
    IOptions<ManagementOptions> options)
    : IPlanApplication
{
    /// <summary>Leading marker of a new-account invoice action (Docs/22 D9).</summary>
    private const string NewAccountActionPrefix = "n|";

    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<IPlanStateRepository> PlanRepo { get; } = plan_repo;
    private Lazy<IPlanUsageReader> PlanUsageReader { get; } = plan_usage_reader;
    private IPriceCalculator PriceCalc { get; } = price_calc;
    private Lazy<IWalletRepository> WalletRepo { get; } = wallet_repo;
    private Lazy<IPanelUserRepository> PanelUserRepo { get; } = panel_user_repo;
    private Lazy<IVpnAccountRepository> VpnAccountRepo { get; } = vpn_account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<ISessionRadiusSyncService> SessionRadiusSrv { get; } = session_radius_srv;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IServerManagementService> ServerMngSrv { get; } = server_mng_srv;
    private Lazy<IBillingApplication> BillingApp { get; } = billing_app;
    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;
    private Lazy<IJobContext> JobContext { get; } = job_context;
    private Lazy<IEmailService> EmailSrv { get; } = email_srv;
    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<ApiResult<List<RealmChoiceModel>>> GetRealms()
    {
        var states = await ServerMngSrv.Value.GetRealmsCapacityState();

        var result = states.Select(state => new RealmChoiceModel
            {
                Id = state.RealmId,
                Name = state.Name,
                IsSelectable = state.IsSelectableForAssignment,
            })
            .ToList();

        return ApiResult<List<RealmChoiceModel>>.Success(result);
    }

    public async Task<ApiResult<string>> SuggestUsername()
    {
        var actor = await SelfServiceGuard.Value.ResolveActor();

        return ApiResult<string>.Success(await VpnAccountRepo.Value.SuggestUsername(actor.Username));
    }

    public async Task<ApiResult<PlanStateModel>> GetPlanState(int account_id)
    {
        await GetVisiblePlanAccount(account_id);

        var state = await PlanRepo.Value.GetPlanState(account_id);

        Log.Information(
            "[user: {0}] session state: (account:{1}, user-count:{2}, data-left:{3}, total-data:{4}, time-left:{5}-{6})",
            JobContext.Value.Username, account_id, state?.SimultaneousUser,
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

    public async Task<ApiResult<PlanInfoModel>> GetPlanInfo(int account_id)
    {
        var account = await GetVisiblePlanAccount(account_id);

        var renew = await PlanRepo.Value.GetPlanState(account.Id);
        var data_plane_info = renew == null
            ? null
            : await ServerMngSrv.Value.GetDataPlaneInfo(account.RealmId);

        var (stale, _) = await LoadStaleness(account);

        return ApiResult<PlanInfoModel>.Success(new PlanInfoModel
        {
            Target = account.Username,
            AccountId = account.Id,
            RealmId = account.RealmId,
            RequiresRealmChoice = stale,
            SimultaneousUserCount = renew?.SimultaneousUser,
            Days = renew?.TimeLimitInDays,
            Gigabytes = renew?.GetTrafficLimitInGig(),
            Kind = data_plane_info?.Kind,
            SupportsOvpnCert = data_plane_info?.SupportsOvpnCert,
            SupportsPasswordChange = data_plane_info?.SupportsPasswordChange,
            SupportsCredentialRotation = data_plane_info?.SupportsCredentialRotation,
        });
    }

    public async Task<ApiResult<EstimateResult>> Estimate(int account_id, byte users, short days, int gigabytes)
    {
        await SelfServiceGuard.Value.EnsureSelfService();

        var account = await GetRenewableAccount(account_id);
        var owner = await GetOwner(account);

        var renew = new RenewalEntity
        {
            VpnAccountId = account.Id,
            SimultaneousUser = users,
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
        };

        if (renew.RenewalValidation(owner, out var user_exception))
        {
            throw user_exception;
        }

        var result = await PriceCalc.CalculatePrice(owner.CalculationMethod ?? 0, users,
            renew.TimeLimitInDays ?? days, gigabytes);

        return ApiResult<EstimateResult>.Success(new EstimateResult
        {
            Days = renew.TimeLimitInDays,
            Gigabytes = renew.TrafficLimit / StaticValues.BytesInGigLong,
            SimultaneousUserCount = renew.SimultaneousUser,
            Price = result,
        });
    }

    public async Task<ApiResult<RenewalResult>> Renewal(int account_id, byte count, short days, int gigabytes, int? realm_id)
    {
        // D4: the invoice and the wallet debit always belong to the paying
        // actor; the renewal itself is applied to the target account.
        var payer = await SelfServiceGuard.Value.ResolvePayingActor();

        var account = await GetRenewableAccount(account_id);

        JobContext.Value.InjectJobContext(payer.Id, payer.Username, account.Username);

        var (stale, _) = await LoadStaleness(account);

        if (stale && realm_id == null)
        {
            throw new UserException("اکانت شما مدتی غیرفعال بوده است؛ برای تمدید ابتدا سرور (realm) را انتخاب کنید!",
                $"Realm re-selection is required for a stale account (Docs/22 D6): account-id={account.Id}, user={account.Username}");
        }

        var renewal = new RenewalEntity
        {
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            SimultaneousUser = count,
        };

        if (renewal.RenewalValidation(payer, out var validation_exception))
        {
            Log.Information(@"[user: {0}] Plan renewal request rejected before invoice:
    request=(account:{1}, count:{2}, days={3}, gigabytes={4})
    message={5}",
                JobContext.Value.Username, account.Username, count, days, gigabytes, validation_exception.Message);

            throw validation_exception;
        }

        var estimate = await PriceCalc.CalculatePrice(payer.CalculationMethod ?? 0, count,
            renewal.TimeLimitInDays ?? days, gigabytes);

        var action = BuildAction(account.Username, count, days, gigabytes, realm_id);

        var invoice_code = await BillingApp.Value.IssuePlanInvoice(estimate,
            action, renewal.GetPlanTitle(), account.Id);

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
            CurrentPrice = await WalletRepo.Value.GetBalance(payer.Id),
            InvoiceCode = invoice_code.Data,
        });
    }

    public async Task<ApiResult<RenewalResult>> NewAccount(string? username, int realm_id, byte count, short days, int gigabytes, string? target = null)
    {
        var payer = await SelfServiceGuard.Value.ResolvePayingActor();

        // Docs/20 on-behalf: the account belongs to the target user, the
        // invoice to the paying actor (Docs/22 D8)
        var owner = string.IsNullOrWhiteSpace(target)
            ? payer
            : await GetOwnerUser(target);

        var vpn_username = await ResolveNewUsername(owner, username);

        await EnsureRealmSelectable(realm_id);

        var renewal = new RenewalEntity
        {
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            SimultaneousUser = count,
        };

        if (renewal.RenewalValidation(payer, out var validation_exception))
        {
            throw validation_exception;
        }

        var estimate = await PriceCalc.CalculatePrice(payer.CalculationMethod ?? 0, count,
            renewal.TimeLimitInDays ?? days, gigabytes);

        JobContext.Value.InjectJobContext(payer.Id, payer.Username, owner.Username);

        var action = BuildNewAccountAction(vpn_username, count, days, gigabytes, realm_id, owner.Username);

        var invoice_code = await BillingApp.Value.IssuePlanInvoice(estimate,
            action, renewal.GetPlanTitle());

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
            CurrentPrice = await WalletRepo.Value.GetBalance(payer.Id),
            InvoiceCode = invoice_code.Data,
        });
    }

    public async Task<ApiResult<RenewalResult>> SettlePurchase(int payer_user_id, int? payment_id, string action, int? invoice_code = null)
    {
        string? target_name = null;
        string? owner_name = null;
        byte? count = null;
        short? days = null;
        int? gigabytes = null;
        int? realm_id = null;
        var is_new_account = false;

        action.Split('|')
            .Where(x => !string.IsNullOrEmpty(x))
            .Foreach(x =>
            {
                if (x == NewAccountActionPrefix.TrimEnd('|'))
                {
                    is_new_account = true;
                    return;
                }

                switch (x.Last())
                {
                    case 't': target_name = x[..^1]; break;
                    case 'o': owner_name = x[..^1]; break;
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
                    case 'r':
                        if (!int.TryParse(x[..^1], out var parsed_realm))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action realm: {x}");
                        }
                        realm_id = parsed_realm;
                        break;
                }
            });

        if (target_name == null || count == null || days == null || gigabytes == null)
        {
            throw new Exception($"Invalid action={action} [target={target_name}, owner={owner_name}, count={count}, days={days}, gigabytes={gigabytes}]");
        }

        var payer = await PanelUserRepo.Value.GetActiveUser(payer_user_id)
                    ?? throw new UserException("کاربر مورد نظر پیدا نشد!",
                        $"Payer user not found or inactive: id={payer_user_id}");

        if (is_new_account)
        {
            JobContext.Value.InjectJobContext(payer.Id, payer.Username, payer.Username);

            var owner = owner_name == null
                ? payer
                : await GetOwnerUser(owner_name);

            return await PurchaseNewAccount(owner, payer, target_name!, realm_id, payment_id, count.Value, days.Value, gigabytes.Value, invoice_code);
        }

        var account = await VpnAccountRepo.Value.GetAccount(target_name!)
                    ?? throw new UserException("اکانت VPN مورد نظر پیدا نشد!",
                        $"VPN account not found: username={target_name}");

        JobContext.Value.InjectJobContext(payer.Id, payer.Username, account.Username);

        return await RenewalCore(account, payer, realm_id, payment_id, count.Value, days.Value, gigabytes.Value, invoice_code);
    }

    private async Task<ApiResult<RenewalResult>> PurchaseNewAccount(PanelUserEntity owner, PanelUserEntity payer, string vpn_username, int? realm_id,
        int? payment_id, byte count, short days, int gigabytes, int? invoice_code)
    {
        // D9: the VPN account and its first renewal are created together, in
        // one transaction with the radius sync — no VPN account without a plan.
        var resolved_username = await ResolveNewUsername(payer, vpn_username);

        if (realm_id is not int new_realm)
        {
            throw new UserException("انتخاب سرور برای ساخت اکانت جدید الزامی است!",
                $"A new-account settlement without a realm: owner={owner.Username}, username={resolved_username}");
        }

        await EnsureRealmSelectable(new_realm);

        var account = new VpnAccountEntity
        {
            UserId = owner.Id,
            RealmId = new_realm,
            Username = resolved_username,
            VpnPassword = HashHandler.GenerateHashCode(),
        };

        var renewal = new RenewalEntity
        {
            VpnAccountId = account.Id,
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            SimultaneousUser = count,
            WalletCredit = payment_id,
        };

        if (renewal.RenewalValidation(payer, out var user_exception))
        {
            throw user_exception;
        }

        // first plan of the account: expiry starts now, the quota is its own share
        renewal.ExpirationDate = renewal.TimeLimitInDays.HasValue
            ? DateTime.Now.AddDays(renewal.TimeLimitInDays.Value)
            : null;
        renewal.CumulativeTrafficLeft = renewal.TrafficLimit;

        var estimate = await PriceCalc.CalculatePrice(payer.CalculationMethod ?? 0, count,
            renewal.TimeLimitInDays ?? days, gigabytes);
        var balance = await WalletRepo.Value.GetBalance(payer.Id);

        if (payer.CheckMoneyNeed(balance, estimate, out _))
        {
            throw new UserException("موجودی کیف پول برای اجرای این خرید کافی نیست!",
                $"Settlement new-account with money-need: owner={owner.Username}, payer={payer.Username}, username={resolved_username}, balance={balance}, estimate={estimate}");
        }

        Log.Information(@"
[user: {0}] New VPN account purchase:
    payer=(user:{2}, balance:{3})
    request=(username:{1}, realm:{4}, count:{5}, days={6}, gigabytes:{7}, estimate:{8})
",
            JobContext.Value.Username, resolved_username, payer.Username, balance, new_realm,
            count, days, gigabytes, estimate);

        await PanelUserRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            await VpnAccountRepo.Value.Save(account);

            var wallet_debit = new WalletEntity
            {
                AccountId = payer.Id,
                Amount = estimate,
                Direction = BalanceDirection.Debit,
                Status = BalanceStatus.Completed,
                InvoiceCode = invoice_code,
                Description = renewal.GetPlanTitle(),
            };
            await WalletRepo.Value.Save(wallet_debit);

            renewal.VpnAccountId = account.Id;
            renewal.WalletDebit = wallet_debit.Id;

            await RenewalRepo.Value.Save(renewal);

            await AccountRadiusSrv.Value.SyncUserAndActive(account, renewal);

            await PanelUserRepo.Value.DbContext.CommitAsync();
        }
        catch
        {
            try
            {
                await PanelUserRepo.Value.DbContext.RollbackAsync();
            }
            catch (Exception rollback_error)
            {
                Log.Error(rollback_error, "New-account rollback failed: user={0}, username={1}", payer.Username, resolved_username);
            }

            try
            {
                await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
                {
                    Target = payer.Id,
                    VpnAccountId = account.Id,
                    Category = EventCategory.Renewal,
                    Type = EventType.Error,
                    Title = "اکانت جدید",
                    Description = "خطا در ساخت اکانت جدید!",
                });
            }
            catch (Exception history_error)
            {
                Log.Error(history_error, "Saving new-account failure history failed: user={0}, username={1}", payer.Username, resolved_username);
            }

            throw;
        }

        await SaveAccountSuccessHistory(account, "اکانت جدید ساخته شد.", renewal.GetPlanTitle());

        await NotifyRenewalSuccess(owner, renewal, estimate);

        Log.Information("[user: {0}] New VPN account purchase finished: ({1}, {2}, {3}, {4})",
            JobContext.Value.Username, resolved_username, count, days, gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = balance - estimate,
        });
    }

    private async Task<ApiResult<RenewalResult>> RenewalCore(VpnAccountEntity account, PanelUserEntity payer, int? realm_id,
        int? payment_id, byte count, short days, int gigabytes, int? invoice_code)
    {
        if (payer.Status == PanelUserStatus.Deactivated)
        {
            throw new UserException("کاربر غیرفعال است!", $"payer is inactive: target={payer.Username}");
        }

        // D-A: a suspended VPN account keeps portal access but must not renew
        // — reactivation is a management-only operation
        if (account.Status == VpnAccountStatus.Suspended)
        {
            throw new UserException("سرویس VPN شما توسط مدیریت غیرفعال است؛ برای تمدید ابتدا با مدیر خود تماس بگیرید!",
                $"Renewal refused for a suspended VPN account (Docs/20 D13): target={account.Username}");
        }

        var current_state = await PlanRepo.Value.GetPlanState(account.Id);

        var renew = new RenewalEntity
        {
            VpnAccountId = account.Id,
            TimeLimitInDays = days,
            TrafficLimit = gigabytes * StaticValues.BytesInGigLong,
            RateLimitInMeg = null,
            SimultaneousUser = count,
            WalletCredit = payment_id,
        };

        var (stale, _) = await LoadStaleness(account);

        if (stale && realm_id == null)
        {
            throw new UserException("اکانت شما مدتی غیرفعال بوده است؛ برای تمدید ابتدا سرور (realm) را انتخاب کنید!",
                $"Realm re-selection is required for a stale account (Docs/22 D6): account-id={account.Id}, user={account.Username}");
        }

        if (stale && realm_id.HasValue)
        {
            await EnsureRealmSelectable(realm_id.Value);
        }

        if (renew.RenewalValidation(payer, out var user_exception))
        {
            Log.Information(@"[user: {0}] Plan current state:
    request=(target:{1}, count:{2}, days={3}, gigabytes={4})
    message={5}",
                JobContext.Value.Username, account.Username, count, days, gigabytes, user_exception.Message);

            throw user_exception;
        }

        if (current_state != null)
        {
            // D16 (Docs/17): stacked plan state — both values are computed once,
            // right here, and stored on the renewal; the x-ui push and the usage
            // reader only read them. Expiry stacks on the still-running plan, the
            // traffic quota on the cumulative remaining (view TrafficLeft of the
            // current band); NULL means "no limit" on either axis.
            renew.ExpirationDate = renew.TimeLimitInDays.HasValue
                ? LaterOf(DateTime.Now, current_state.ExpirationDate).AddDays(renew.TimeLimitInDays.Value)
                : null;
            renew.CumulativeTrafficLeft = StackRemainingTraffic(current_state, renew.TrafficLimit);
        }
        else
        {
            // the first plan of an account that predates the split or lost its
            // plan rows: treat like a fresh purchase
            renew.ExpirationDate = renew.TimeLimitInDays.HasValue
                ? DateTime.Now.AddDays(renew.TimeLimitInDays.Value)
                : null;
            renew.CumulativeTrafficLeft = renew.TrafficLimit;
        }

        var estimate = await PriceCalc.CalculatePrice(payer.CalculationMethod ?? 0, count,
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
    request=(taget:{1}, count:{2}, days={3}, gigabytes:{4}, estimate:{10})
    current=(count:{5}, left-days:{6}, left-hours:{7}, left-gigabytes:{8})
",
            JobContext.Value.Username,
            account.Username, count, days, gigabytes,
            current_state?.SimultaneousUser, current_state?.TimeLeft?.TotalDays, current_state?.TimeLeft?.Hours,
            current_state?.GetTrafficLeftInGig(),
            balance, estimate,
            payer.Username);

        await VpnAccountRepo.Value.DbContext.BeginTransactionAsync();

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
                VpnAccountId = account.Id,
                Category = EventCategory.Transaction,
                Type = EventType.Information,
                Title = "مالی",
                Description = "از حساب کم شد.",
                Price = -estimate,
            });

            await RenewalRepo.Value.Save(renew);

            if (stale && realm_id is int new_realm)
            {
                // D6: username and stacked state migrate to the user-chosen realm
                await AccountRadiusSrv.Value.MigrateUserRealm(account, new_realm, renew);
                await VpnAccountRepo.Value.Save(account);
            }
            else
            {
                await AccountRadiusSrv.Value.SyncUserAndActive(account, renew);
            }

            await VpnAccountRepo.Value.DbContext.CommitAsync();
        }
        catch
        {
            try
            {
                await VpnAccountRepo.Value.DbContext.RollbackAsync();
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
                    Target = account.UserId,
                    VpnAccountId = account.Id,
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
            if (count < current_state?.SimultaneousUser)
            {
                Log.Information("""
                                [user: {0}] Plan renewal change user count: (closing connections)
                                    change=(taget:{1}, user-count:{2}, to:{3})
                                """,
                    JobContext.Value.Username, account.Username, current_state.SimultaneousUser, count);

                await SessionRadiusSrv.Value.CloseConnectionByUsername(account.RealmId, account.Username);
            }
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Closing connections after renewal commit failed: target={0}", account.Username);
        }

        await SaveAccountSuccessHistory(account, "پلن تمید شد.", renew.GetPlanTitle());

        try
        {
            await ServerMngSrv.Value.CheckUserServerBalance();
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Server capacity check after renewal commit failed: target={0}", account.Username);
        }

        var owner = await PanelUserRepo.Value.GetActiveUser(account.UserId)
                     ?? throw new UserException("کاربر پیدا نشد!", $"Owner not found or inactive: id={account.UserId}");

        await NotifyRenewalSuccess(owner, renew, estimate);

        Log.Information("[user: {0}] Plan renewal finished: ({1}, {2}, {3}, {4})",
            JobContext.Value.Username, account.Username, count, days, gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = balance,
        });
    }

    private async Task SaveAccountSuccessHistory(VpnAccountEntity account, string description, string value)
    {
        try
        {
            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.UserId,
                VpnAccountId = account.Id,
                Category = EventCategory.Renewal,
                Type = EventType.Success,
                Title = "تمدید",
                Description = description,
                Value = value,
            });
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Saving renewal success history failed: target={0}", account.Username);
        }
    }

    private async Task NotifyRenewalSuccess(PanelUserEntity payer, RenewalEntity renew, int estimate)
    {
        try
        {
            // the confirmation goes to the plan owner, not necessarily the payer
            // (Docs/20 payer=actor)
            if (payer.EmailAddress != null)
            {
                await EmailSrv.Value.SendRenewalSuccessEmail(payer.Fullname, payer.EmailAddress,
                    renew.GetPlanTitle(), estimate,
                    renew.ExpirationDate?.ToString("yyyy/MM/dd HH:mm") ?? string.Empty);
            }
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Sending renewal success email failed: target={0}", payer.Username);
        }
    }

    /// <summary>
    /// D6 staleness probe: recorded activity older than
    /// <see cref="ManagementOptions.RealmReassignInactivityDays"/> days. No
    /// recorded activity is NOT stale — a freshly purchased account must renew
    /// without a realm re-pick (the radius-side abandonment sweep, not this
    /// gate, owns never-used accounts).
    /// </summary>
    private async Task<(bool Stale, DateTime? LastActivity)> LoadStaleness(VpnAccountEntity account)
    {
        var current_usage = await PlanUsageReader.Value.GetUsage(account.Id);
        var current_state = await PlanRepo.Value.GetPlanState(account.Id);
        var last_activity = current_usage?.LastActiveTime ?? current_state?.LastConnectTime;

        return (last_activity < DateTime.Now.AddDays(-Options.Value.RealmReassignInactivityDays), last_activity);
    }

    /// <summary>Portal-visible check only — suspended accounts still read their plan state.</summary>
    private async Task<VpnAccountEntity> GetVisiblePlanAccount(int account_id)
    {
        return await VpnAccountRepo.Value.GetVisibleAccount(account_id)
               ?? throw new UserException("اکانت VPN پیدا نشد یا غیرفعال است!",
                   $"VPN account not found or deactivated: id={account_id}");
    }

    private async Task<VpnAccountEntity> GetRenewableAccount(int account_id)
    {
        var account = await GetVisiblePlanAccount(account_id);

        if (account.Status == VpnAccountStatus.Suspended)
        {
            throw new UserException("سرویس VPN شما توسط مدیریت غیرفعال است!",
                $"VPN account is suspended (Docs/20 D13): id={account_id}, user={account.Username}");
        }

        return account;
    }

    private Task<PanelUserEntity> GetOwner(VpnAccountEntity account)
    {
        return PanelUserRepo.Value.GetActiveUser(account.UserId)
               ?? throw new UserException("کاربر پیدا نشد!", $"Owner not found or inactive: id={account.UserId}");
    }

    private async Task<PanelUserEntity> GetOwnerUser(string username)
    {
        var owner = await PanelUserRepo.Value.GetUser(username)
                    ?? throw new UserException("کاربر مورد نظر پیدا نشد!", $"Owner user not found: {username}");

        if (owner.Status == PanelUserStatus.Deactivated)
        {
            throw new UserException("کاربر مورد نظر غیرفعال است!", $"Owner user is inactive: {username}");
        }

        return owner;
    }

    /// <summary>
    /// Resolves the VPN username for a new account (D3): validates the
    /// user-supplied value or falls back to the system suggestion. The
    /// uniqueness re-check happens at insert time through the unique index —
    /// a concurrent claim surfaces as a friendly 409.
    /// </summary>
    private async Task<string> ResolveNewUsername(PanelUserEntity payer, string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return await VpnAccountRepo.Value.SuggestUsername(payer.Username);
        }

        AccountBusiness.ValidateVpnUsername(payer.Username, username);

        if (await VpnAccountRepo.Value.GetAccount(username) != null)
        {
            throw new UserException("این نام کاربری VPN قبلا استفاده شده است!",
                $"VPN username already taken (Docs/22 D3): {username}", 409);
        }

        return username;
    }

    private async Task EnsureRealmSelectable(int realm_id)
    {
        var realms = await ServerMngSrv.Value.GetRealmsCapacityState();
        var realm = realms.FirstOrDefault(state => state.RealmId == realm_id);

        if (realm is not { IsSelectableForAssignment: true })
        {
            throw new UserException("این سرور در حال حاضر قابل انتخاب نیست؛ لطفاً سرور دیگری انتخاب کنید!",
                $"Realm is not selectable (Docs/22 D4): realm-id={realm_id}");
        }
    }

    private static string BuildAction(string target_name, byte count, short days, int gigabytes, int? realm_id,
        bool is_new_account = false)
    {
        var parts = new List<string>();

        if (is_new_account)
        {
            parts.Add(NewAccountActionPrefix.TrimEnd('|'));
        }

        parts.Add($"{target_name}t");
        parts.Add($"{count}u");
        parts.Add($"{days}d");
        parts.Add($"{gigabytes}g");

        if (realm_id.HasValue)
        {
            parts.Add($"{realm_id.Value}r");
        }

        return string.Join("|", parts);
    }

    /// <summary>
    /// New-account action with the on-behalf owner token: the VPN username
    /// alone cannot identify the owner across prefix overlaps (user "al" vs
    /// "ali" both matching "ali1…", Docs/22 D3).
    /// </summary>
    private static string BuildNewAccountAction(string vpn_username, byte count, short days, int gigabytes,
        int realm_id, string owner_username)
    {
        return NewAccountActionPrefix.TrimEnd('|') + "|" +
               $"{vpn_username}t|" +
               $"{owner_username}o|" +
               $"{count}u|" +
               $"{days}d|" +
               $"{gigabytes}g|" +
               $"{realm_id}r";
    }

    private static DateTime LaterOf(DateTime now, DateTime? current_expiry)
    {
        return current_expiry.HasValue && current_expiry.Value > now ? current_expiry.Value : now;
    }

    /// <summary>
    /// D16 (Docs/17): the traffic quota carried into the new renewal — the
    /// cumulative remaining of the current band (view TrafficLeft; a NULL on a
    /// limited band means "no usage recorded yet", i.e. the full quota) plus
    /// this renewal's share, clamped at zero for overdrawn plans. A NULL
    /// previous remaining means an unlimited current plan, so the stack is
    /// unlimited; an account with no plan at all starts from its own share.
    /// </summary>
    private static long? StackRemainingTraffic(PlanStateEntity? current_state, long? new_share)
    {
        if (current_state == null)
        {
            return new_share;
        }

        var has_current_plan = current_state.TrafficLimit.HasValue || current_state.TimeLimitInDays.HasValue;

        if (!has_current_plan)
        {
            return new_share;
        }

        var remaining = current_state.TrafficLeft ?? current_state.TrafficLimit;

        return remaining.HasValue ? Math.Max(0, remaining.Value) + new_share : null;
    }
}
