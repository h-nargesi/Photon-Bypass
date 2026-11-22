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
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanApplication(
    Lazy<IRenewalRepository> renewal_repo,
    Lazy<IPlanStateRepository> plan_repo,
    Lazy<IPlanRepository> plan_catalog_repo,
    Lazy<IUserGroupRepository> group_repo,
    Lazy<IPlanUsageReader> plan_usage_reader,
    IPriceCalculator price_calc,
    Lazy<IWalletRepository> wallet_repo,
    Lazy<IPanelUserRepository> panel_user_repo,
    Lazy<IVpnAccountRepository> vpn_account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<ISessionRadiusSyncService> session_radius_srv,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IServerManagementService> server_mng_srv,
    Lazy<IServerRepository> server_repo,
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
    private Lazy<IPlanRepository> PlanCatalogRepo { get; } = plan_catalog_repo;
    private Lazy<IUserGroupRepository> GroupRepo { get; } = group_repo;
    private Lazy<IPlanUsageReader> PlanUsageReader { get; } = plan_usage_reader;
    private IPriceCalculator PriceCalc { get; } = price_calc;
    private Lazy<IWalletRepository> WalletRepo { get; } = wallet_repo;
    private Lazy<IPanelUserRepository> PanelUserRepo { get; } = panel_user_repo;
    private Lazy<IVpnAccountRepository> VpnAccountRepo { get; } = vpn_account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<ISessionRadiusSyncService> SessionRadiusSrv { get; } = session_radius_srv;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IServerManagementService> ServerMngSrv { get; } = server_mng_srv;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<IBillingApplication> BillingApp { get; } = billing_app;
    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;
    private Lazy<IJobContext> JobContext { get; } = job_context;
    private Lazy<IEmailService> EmailSrv { get; } = email_srv;
    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<ApiResult<List<RealmChoiceModel>>> GetRealms()
    {
        var result = (await ServerMngSrv.Value.GetRealmsCapacityState())
            .Select(state => new RealmChoiceModel
            {
                Id = state.RealmId,
                Name = state.Name,
                DisplayName = state.DisplayName,
                IsSelectable = state.IsSelectableForAssignment,
            })
            .ToList();

        return ApiResult<List<RealmChoiceModel>>.Success(result);
    }

    public async Task<ApiResult<List<PlanCatalogModel>>> GetCatalog(string? target = null)
    {
        var actor = await SelfServiceGuard.Value.ResolveActor();

        var target_user = string.IsNullOrWhiteSpace(target) || target == actor.Username
            ? actor
            : await GetOwnerUser(target);

        // D7: the pricing group always rides with the wallet — the admin
        // exception resolves to the target user, everyone else to the actor
        var billing_user = ResolveBillingUser(actor, target_user);

        var plans = await PlanCatalogRepo.Value.GetActiveByGroup(billing_user.UserGroupId);

        if (plans.Count == 0)
        {
            return ApiResult<List<PlanCatalogModel>>.Success([]);
        }

        var capacity = (await ServerMngSrv.Value.GetRealmsCapacityState()).ToDictionary(state => state.RealmId);

        var result = new List<PlanCatalogModel>();

        foreach (var plan in plans)
        {
            var realm_ids = await PlanCatalogRepo.Value.GetRealmIds(plan.Id);

            result.Add(new PlanCatalogModel
            {
                Id = plan.Id,
                Title = plan.Title,
                UsersBase = plan.UsersBase,
                UsersCap = plan.UsersCap,
                DaysBase = plan.DaysBase,
                DaysCap = plan.DaysCap,
                TrafficBase = plan.TrafficBase,
                TrafficCap = plan.TrafficCap,
                RateBase = plan.RateBase,
                RateCap = plan.RateCap,
                Realms = [.. capacity.Values
                    .Where(state => realm_ids.Contains(state.RealmId))
                    .Select(state => new RealmChoiceModel
                    {
                        Id = state.RealmId,
                        Name = state.Name,
                        DisplayName = state.DisplayName,
                        IsSelectable = state.IsSelectableForAssignment,
                    })],
            });
        }

        return ApiResult<List<PlanCatalogModel>>.Success(result);
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

    public async Task<ApiResult<EstimateResult>> Estimate(int account_id, PurchaseRequest request)
    {
        await SelfServiceGuard.Value.EnsureSelfService();

        var account = await GetRenewableAccount(account_id);
        var owner = await GetOwner(account);
        var actor = await SelfServiceGuard.Value.ResolveActor();

        // D7: the estimate prices with the wallet owner's group (the old code
        // used the account owner's calculator pointer — inconsistent with the
        // purchase path; both now ride with the billing user)
        var billing_user = ResolveBillingUser(actor, owner);

        var plan = await LoadPlanForPurchase(request.PlanId, billing_user.UserGroupId);

        if (plan.ValidateChoices(request.SimultaneousUserCount, request.Days, request.Gigabytes, request.RateLimitMeg,
                out var choice_exception))
        {
            throw choice_exception;
        }

        var (limits, output) = await ComposePurchase(plan, billing_user, request, account_id);

        return ApiResult<EstimateResult>.Success(new EstimateResult
        {
            Price = output.Price,
            Days = limits.Days,
            Gigabytes = limits.Gigabytes,
            SimultaneousUserCount = limits.Users,
            RateLimitMeg = limits.RateLimitMeg,
            Warnings = BuildStackingWarnings(await PlanRepo.Value.GetPlanState(account_id), limits),
        });
    }

    public async Task<ApiResult<RenewalResult>> Renewal(int account_id, PurchaseRequest request)
    {
        // Docs/20 D4 + Docs/24 D7: the invoice and the wallet debit belong to
        // the paying actor — except the admin on-behalf exception, where both
        // ride with the target user.
        var actor = await SelfServiceGuard.Value.ResolvePayingActor();

        var account = await GetRenewableAccount(account_id);
        var owner = await GetOwner(account);
        var billing_user = ResolveBillingUser(actor, owner);

        JobContext.Value.InjectJobContext(billing_user.Id, actor.Username, account.Username);

        var (stale, _) = await LoadStaleness(account);

        if (stale && request.RealmId == null)
        {
            throw new UserException("اکانت شما مدتی غیرفعال بوده است؛ برای تمدید ابتدا سرور (realm) را انتخاب کنید!",
                $"Realm re-selection is required for a stale account (Docs/22 D6): account-id={account.Id}, user={account.Username}");
        }

        var plan = await LoadPlanForPurchase(request.PlanId, billing_user.UserGroupId);

        if (plan.ValidateChoices(request.SimultaneousUserCount, request.Days, request.Gigabytes, request.RateLimitMeg,
                out var choice_exception))
        {
            Log.Information(@"[user: {0}] Plan renewal request rejected before invoice:
    request=(account:{1}, plan:{2}, limits=({3}u, {4}d, {5}g, {6}b))
    message={7}",
                JobContext.Value.Username, account.Username, plan.Id,
                request.SimultaneousUserCount, request.Days, request.Gigabytes, request.RateLimitMeg,
                choice_exception.Message);

            throw choice_exception;
        }

        if (request.RealmId.HasValue)
        {
            await EnsureRealmPurchasable(request.RealmId.Value, plan);
        }

        await EnsureRateLimitEnforceable(request.RealmId ?? account.RealmId,
            plan.RateBase.HasValue || plan.RateCap.HasValue);

        var (limits, output) = await ComposePurchase(plan, billing_user, request, account.Id);

        var action = BuildAction(account.Username, plan.Id, limits, request.RealmId,
            actor_id: actor.Id != billing_user.Id ? actor.Id : null);

        var invoice_code = await BillingApp.Value.IssuePlanInvoice(output.Price,
            action, plan.Title, account.Id);

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
            CurrentPrice = await WalletRepo.Value.GetBalance(billing_user.Id),
            InvoiceCode = invoice_code.Data,
        });
    }

    public async Task<ApiResult<RenewalResult>> NewAccount(NewAccountRequest request)
    {
        var actor = await SelfServiceGuard.Value.ResolvePayingActor();

        // Docs/20 on-behalf: the account belongs to the target user, the
        // invoice to the paying actor (Docs/22 D8) — inverted by the admin
        // exception (Docs/24 D7)
        var owner = string.IsNullOrWhiteSpace(request.Target)
            ? actor
            : await GetOwnerUser(request.Target);

        var billing_user = ResolveBillingUser(actor, owner);

        var vpn_username = await ResolveNewUsername(owner, request.Username);

        if (request.RealmId is not int realm_id)
        {
            throw new UserException("انتخاب سرور برای ساخت اکانت جدید الزامی است!",
                $"New-account request without a realm: owner={owner.Username}, username={vpn_username}");
        }

        var plan = await LoadPlanForPurchase(request.PlanId, billing_user.UserGroupId);

        if (plan.ValidateChoices(request.SimultaneousUserCount, request.Days, request.Gigabytes, request.RateLimitMeg,
                out var choice_exception))
        {
            throw choice_exception;
        }

        await EnsureRealmPurchasable(realm_id, plan);

        await EnsureRateLimitEnforceable(realm_id, plan.RateBase.HasValue || plan.RateCap.HasValue);

        var (limits, output) = await ComposePurchase(plan, billing_user, request, account_id: null);

        JobContext.Value.InjectJobContext(billing_user.Id, actor.Username, owner.Username);

        var action = BuildNewAccountAction(vpn_username, plan.Id, limits, realm_id, owner.Username,
            actor_id: actor.Id != billing_user.Id ? actor.Id : null);

        var invoice_code = await BillingApp.Value.IssuePlanInvoice(output.Price, action, plan.Title);

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
            CurrentPrice = await WalletRepo.Value.GetBalance(billing_user.Id),
            InvoiceCode = invoice_code.Data,
        });
    }

    public async Task<ApiResult<RenewalResult>> SettlePurchase(int payer_user_id, int? payment_id, string action, int price,
        int? invoice_code = null)
    {
        string? target_name = null;
        string? owner_name = null;
        int? plan_id = null;
        byte? count = null;
        short? days = null;
        int? gigabytes = null;
        int? rate = null;
        int? realm_id = null;
        int? actor_id = null;
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
                    case 'p':
                        if (!int.TryParse(x[..^1], out var parsed_plan))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action plan: {x}");
                        }
                        plan_id = parsed_plan;
                        break;
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
                    case 'b':
                        if (!int.TryParse(x[..^1], out var parsed_rate))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action rate: {x}");
                        }
                        rate = parsed_rate;
                        break;
                    case 'r':
                        if (!int.TryParse(x[..^1], out var parsed_realm))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action realm: {x}");
                        }
                        realm_id = parsed_realm;
                        break;
                    case 'a':
                        if (!int.TryParse(x[..^1], out var parsed_actor))
                        {
                            throw new UserException("درخواست تمدید نامعتبر است!", $"Invalid renewal action actor: {x}");
                        }
                        actor_id = parsed_actor;
                        break;
                }
            });

        if (target_name == null || plan_id == null)
        {
            throw new Exception($"Invalid action={action} [target={target_name}, plan={plan_id}, owner={owner_name}, " +
                                $"count={count}, days={days}, gigabytes={gigabytes}, rate={rate}]");
        }

        // absent axis tokens are unlimited (Docs/24 D9); an all-unlimited
        // purchase was already refused at issuance — keep the guard cheap and
        // local for hand-seeded fixtures
        var limits = new PurchaseLimits(count, days, gigabytes, rate);

        if (RenewalBusiness.HasNoLimit(count, days,
                gigabytes.HasValue ? gigabytes * StaticValues.BytesInGigLong : null, rate, out var limit_exception))
        {
            throw limit_exception;
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

            return await PurchaseNewAccount(owner, payer, target_name!, realm_id, payment_id, plan_id.Value, limits,
                actor_id, price, invoice_code);
        }

        var account = await VpnAccountRepo.Value.GetAccount(target_name!)
                    ?? throw new UserException("اکانت VPN مورد نظر پیدا نشد!",
                        $"VPN account not found: username={target_name}");

        JobContext.Value.InjectJobContext(payer.Id, payer.Username, account.Username);

        return await RenewalCore(account, payer, realm_id, payment_id, plan_id.Value, limits, actor_id, price, invoice_code);
    }

    private async Task<ApiResult<RenewalResult>> PurchaseNewAccount(PanelUserEntity owner, PanelUserEntity payer, string vpn_username, int? realm_id,
        int? payment_id, int plan_id, PurchaseLimits limits, int? actor_id, int price, int? invoice_code)
    {
        // D9: the VPN account and its first renewal are created together, in
        // one transaction with the radius sync — no VPN account without a plan.
        var resolved_username = await ResolveNewUsername(owner, vpn_username);

        if (realm_id is not int new_realm)
        {
            throw new UserException("انتخاب سرور برای ساخت اکانت جدید الزامی است!",
                $"A new-account settlement without a realm: owner={owner.Username}, username={resolved_username}");
        }

        await EnsureRealmSelectable(new_realm);

        var plan = await LoadPlan(plan_id);

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
            PlanId = plan.Id,
            ActorId = actor_id ?? payer.Id,
            SimultaneousUser = limits.Users,
            TimeLimitInDays = limits.Days,
            TrafficLimit = limits.Gigabytes.HasValue ? limits.Gigabytes * StaticValues.BytesInGigLong : null,
            RateLimitInMeg = limits.RateLimitMeg,
            WalletCredit = payment_id,
        };

        // first plan of the account: expiry starts now, the quota is its own share
        renewal.ExpirationDate = renewal.TimeLimitInDays.HasValue
            ? DateTime.Now.AddDays(renewal.TimeLimitInDays.Value)
            : null;
        renewal.CumulativeTrafficLeft = renewal.TrafficLimit;

        var balance = await WalletRepo.Value.GetBalance(payer.Id);

        CheckSettlementMoneyNeed(payer, balance, price, actor_id,
            $"Settlement new-account: owner={owner.Username}, payer={payer.Username}, username={resolved_username}");

        Log.Information(@"
[user: {0}] New VPN account purchase:
    payer=(user:{2}, balance:{3})
    request=(username:{1}, realm:{4}, plan:{5}, limits=({6}u, {7}d, {8}g, {9}b), price:{10})
",
            JobContext.Value.Username, resolved_username, payer.Username, balance, new_realm,
            plan.Id, limits.Users, limits.Days, limits.Gigabytes, limits.RateLimitMeg, price);

        await PanelUserRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            await VpnAccountRepo.Value.Save(account);

            var wallet_debit = new WalletEntity
            {
                AccountId = payer.Id,
                Amount = price,
                Direction = BalanceDirection.Debit,
                Status = BalanceStatus.Completed,
                InvoiceCode = invoice_code,
                Description = plan.Title,
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

        await SaveAccountSuccessHistory(account, "اکانت جدید ساخته شد.", plan.Title);

        await NotifyRenewalSuccess(owner, renewal, price, plan.Title);

        Log.Information("[user: {0}] New VPN account purchase finished: ({1}, {2}, {3}, {4})",
            JobContext.Value.Username, resolved_username, limits.Users, limits.Days, limits.Gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = balance - price,
        });
    }

    private async Task<ApiResult<RenewalResult>> RenewalCore(VpnAccountEntity account, PanelUserEntity payer, int? realm_id,
        int? payment_id, int plan_id, PurchaseLimits limits, int? actor_id, int price, int? invoice_code)
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

        var plan = await LoadPlan(plan_id);

        var renew = new RenewalEntity
        {
            VpnAccountId = account.Id,
            PlanId = plan.Id,
            ActorId = actor_id ?? payer.Id,
            SimultaneousUser = limits.Users,
            TimeLimitInDays = limits.Days,
            TrafficLimit = limits.Gigabytes.HasValue ? limits.Gigabytes * StaticValues.BytesInGigLong : null,
            RateLimitInMeg = limits.RateLimitMeg,
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

        if (HasCurrentBand(current_state))
        {
            // D16 (Docs/17) + D5 (Docs/24): stacked plan state — computed once,
            // right here, and stored on the renewal; the x-ui push and the
            // usage reader only read them. Expiry stacks on the still-running
            // plan, the traffic quota on the cumulative remaining (view
            // TrafficLeft of the current band); on either axis an unlimited
            // side dominates the stack.
            renew.ExpirationDate = ComposeStackedExpiry(current_state!, renew.TimeLimitInDays);
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

        var balance = await WalletRepo.Value.GetBalance(payer.Id);

        CheckSettlementMoneyNeed(payer, balance, price, actor_id,
            $"Settlement renewal: payer={payer.Username}, target={account.Username}");

        Log.Information(@"
[user: {0}] Plan current state:
    account=(user:{1}, balance:{2})
    request=(taget:{3}, plan:{4}, limits=({5}u, {6}d, {7}g, {8}b), price:{9})
    current=(count:{10}, left-days:{11}, left-hours:{12}, left-gigabytes:{13})
",
            JobContext.Value.Username,
            payer.Username, balance,
            account.Username, plan.Id, limits.Users, limits.Days, limits.Gigabytes, limits.RateLimitMeg, price,
            current_state?.SimultaneousUser, current_state?.TimeLeft?.TotalDays, current_state?.TimeLeft?.Hours,
            current_state?.GetTrafficLeftInGig());

        await VpnAccountRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            var wallet_debit = new WalletEntity
            {
                AccountId = payer.Id,
                Amount = price,
                Direction = BalanceDirection.Debit,
                Status = BalanceStatus.Completed,
                InvoiceCode = invoice_code,
                Description = plan.Title,
            };
            await WalletRepo.Value.Save(wallet_debit);

            balance -= price;

            renew.WalletDebit = wallet_debit.Id;

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = payer.Id,
                VpnAccountId = account.Id,
                Category = EventCategory.Transaction,
                Type = EventType.Information,
                Title = "مالی",
                Description = "از حساب کم شد.",
                Price = -price,
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
            if (limits.Users < current_state?.SimultaneousUser)
            {
                Log.Information("""
                                [user: {0}] Plan renewal change user count: (closing connections)
                                    change=(taget:{1}, user-count:{2}, to:{3})
                                """,
                    JobContext.Value.Username, account.Username, current_state.SimultaneousUser, limits.Users);

                await SessionRadiusSrv.Value.CloseConnectionByUsername(account.RealmId, account.Username);
            }
        }
        catch (Exception post_commit_error)
        {
            Log.Error(post_commit_error,
                "Closing connections after renewal commit failed: target={0}", account.Username);
        }

        await SaveAccountSuccessHistory(account, "پلن تمید شد.", plan.Title);

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

        // informative email: the plan title plus the composed axis summary
        await NotifyRenewalSuccess(owner, renew, price, $"{plan.Title} ({renew.GetPlanTitle()})");

        Log.Information("[user: {0}] Plan renewal finished: ({1}, {2}, {3}, {4})",
            JobContext.Value.Username, account.Username, limits.Users, limits.Days, limits.Gigabytes);

        return ApiResult<RenewalResult>.Success(new RenewalResult
        {
            CurrentPrice = balance,
        });
    }

    /// <summary>
    /// D7: a purchase issued by an admin on behalf of another user (an actor
    /// token different from the invoice owner) may overdraw the target wallet
    /// — the admin is the gifting authority, and the wallet-threshold rule
    /// stays the antidote. Every other payer faces the ordinary money-need
    /// refusal.
    /// </summary>
    private static void CheckSettlementMoneyNeed(PanelUserEntity payer, int balance, int price, int? actor_id,
        string log_context)
    {
        var admin_on_behalf = actor_id.HasValue && actor_id.Value != payer.Id;

        if (admin_on_behalf)
        {
            Log.Information("Admin on-behalf settlement skips the money-need gate (Docs/24 D7): {0}, balance={1}, price={2}",
                log_context, balance, price);
            return;
        }

        if (payer.CheckMoneyNeed(balance, price, out _))
        {
            throw new UserException("موجودی کیف پول برای اجرای این خرید کافی نیست!",
                $"Settlement with money-need: {log_context}, balance={balance}, price={price}");
        }
    }

    /// <summary>
    /// Runs the calculator and composes the final four-axis limitation
    /// (Docs/24 D2): validated buyer choices on plan-defined axes, calculator
    /// values on undefined axes. Refuses an all-unlimited composition.
    /// </summary>
    private async Task<(PurchaseLimits Limits, PriceCalculationOutput Output)> ComposePurchase(PlanEntity plan,
        PanelUserEntity billing_user, PurchaseRequest request, int? account_id)
    {
        var current_state = account_id.HasValue
            ? await PlanRepo.Value.GetPlanState(account_id.Value)
            : null;

        var group = await GroupRepo.Value.Find(billing_user.UserGroupId)
                    ?? await GroupRepo.Value.GetDefault();

        var input = new PriceCalculationInput
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            GroupTitle = group.Title,
            Users = request.SimultaneousUserCount,
            Days = request.Days,
            Gigabytes = request.Gigabytes,
            RateLimitMeg = request.RateLimitMeg,
            UsersAxis = new PlanAxis { Base = plan.UsersBase, Cap = plan.UsersCap },
            DaysAxis = new PlanAxis { Base = plan.DaysBase, Cap = plan.DaysCap },
            TrafficAxis = new PlanAxis { Base = plan.TrafficBase, Cap = plan.TrafficCap },
            RateAxis = new PlanAxis { Base = plan.RateBase, Cap = plan.RateCap },
            Current = current_state == null
                ? null
                : new CurrentAccountState
                {
                    SimultaneousUser = current_state.SimultaneousUser,
                    TimeLimitInDays = current_state.TimeLimitInDays,
                    TrafficLimit = current_state.TrafficLimit,
                    RateLimitInMeg = current_state.RateLimitInMeg,
                    ExpirationDate = current_state.ExpirationDate,
                    TrafficLeft = current_state.TrafficLeft,
                    TrafficUsed = current_state.TrafficUsed,
                },
        };

        var output = await PriceCalc.Calculate(input);

        var limits = new PurchaseLimits(
            plan.UsersBase.HasValue ? request.SimultaneousUserCount : output.Users,
            plan.DaysBase.HasValue ? request.Days : output.Days,
            plan.TrafficBase.HasValue ? request.Gigabytes : output.Gigabytes,
            plan.RateBase.HasValue ? request.RateLimitMeg : output.RateLimitMeg);

        if (RenewalBusiness.HasNoLimit(limits.Users, limits.Days,
                limits.Gigabytes.HasValue ? limits.Gigabytes * StaticValues.BytesInGigLong : null, limits.RateLimitMeg,
                out var limit_exception))
        {
            throw limit_exception;
        }

        return (limits, output);
    }

    private async Task<PlanEntity> LoadPlanForPurchase(int plan_id, int group_id)
    {
        var plan = await LoadPlan(plan_id);

        if (!plan.IsActive || plan.GroupId != group_id)
        {
            // generic refusal — no disclosure of other groups' plans (Docs/24 §5)
            throw new UserException("این پلن برای شما قابل خریداری نیست!",
                $"Plan not purchasable for the group (Docs/24 D7): plan-id={plan_id}, plan-group={plan.GroupId}, " +
                $"billing-group={group_id}, active={plan.IsActive}",
                403);
        }

        return plan;
    }

    private async Task<PlanEntity> LoadPlan(int plan_id)
    {
        return await PlanCatalogRepo.Value.Find(plan_id)
               ?? throw new UserException("پلن مورد نظر پیدا نشد!", $"Plan not found: id={plan_id}");
    }

    /// <summary>
    /// D7: the group and the wallet always ride together — the admin acting
    /// on another user prices and pays with that user; everyone else (a
    /// manager on behalf included) uses their own.
    /// </summary>
    private static PanelUserEntity ResolveBillingUser(PanelUserEntity actor, PanelUserEntity owner)
    {
        return actor.Role == AccountRole.Admin && owner.Id != actor.Id
            ? owner
            : actor;
    }

    /// <summary>
    /// D10: the x-ui API has no per-client bandwidth limit, so a rate-limited
    /// purchase on an x-ui realm is refused instead of silently dropping the
    /// limit.
    /// </summary>
    private async Task EnsureRateLimitEnforceable(int realm_id, bool has_rate_axis)
    {
        if (!has_rate_axis) return;

        var clusters = await ServerRepo.Value.GetAllActiveNasInRealm([realm_id]);

        if (clusters.TryGetValue(realm_id, out var servers) &&
            servers.Any(server => (server.Features & ServerFeature.Xui) != 0))
        {
            throw new UserException("پلن‌های با محدودیت پهنای باند روی این سرور قابل خریداری نیستند!",
                $"Rate-limited plan on an x-ui realm refused (Docs/24 D10): realm-id={realm_id}");
        }
    }

    private async Task EnsureRealmPurchasable(int realm_id, PlanEntity plan)
    {
        var plan_realms = await PlanCatalogRepo.Value.GetRealmIds(plan.Id);

        if (!plan_realms.Contains(realm_id))
        {
            throw new UserException("این سرور برای پلن انتخابی شما در دسترس نیست؛ لطفاً سرور دیگری انتخاب کنید!",
                $"Realm is not part of the plan's realm set (Docs/24 §6): realm-id={realm_id}, plan-id={plan.Id}");
        }

        await EnsureRealmSelectable(realm_id);
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

    private async Task NotifyRenewalSuccess(PanelUserEntity payer, RenewalEntity renew, int price, string plan_title)
    {
        try
        {
            // the confirmation goes to the plan owner, not necessarily the payer
            // (Docs/20 payer=actor / Docs/24 D7)
            if (payer.EmailAddress != null)
            {
                await EmailSrv.Value.SendRenewalSuccessEmail(payer.Fullname, payer.EmailAddress,
                    plan_title, price,
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
    private async Task<string> ResolveNewUsername(PanelUserEntity owner, string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return await VpnAccountRepo.Value.SuggestUsername(owner.Username);
        }

        AccountBusiness.ValidateVpnUsername(owner.Username, username);

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

    /// <summary>
    /// D9: the invoice action freezes the purchase — target, plan and the
    /// composed four-axis limitation (absent token = unlimited). Settlement
    /// never re-runs the calculator.
    /// </summary>
    private static string BuildAction(string target_name, int plan_id, PurchaseLimits limits, int? realm_id,
        int? actor_id = null, bool is_new_account = false)
    {
        var parts = new List<string>();

        if (is_new_account)
        {
            parts.Add(NewAccountActionPrefix.TrimEnd('|'));
        }

        parts.Add($"{target_name}t");
        parts.Add($"{plan_id}p");

        if (limits.Users.HasValue) parts.Add($"{limits.Users.Value}u");
        if (limits.Days.HasValue) parts.Add($"{limits.Days.Value}d");
        if (limits.Gigabytes.HasValue) parts.Add($"{limits.Gigabytes.Value}g");
        if (limits.RateLimitMeg.HasValue) parts.Add($"{limits.RateLimitMeg.Value}b");
        if (realm_id.HasValue) parts.Add($"{realm_id.Value}r");
        if (actor_id.HasValue) parts.Add($"{actor_id.Value}a");

        return string.Join("|", parts);
    }

    /// <summary>
    /// New-account action with the on-behalf owner token: the VPN username
    /// alone cannot identify the owner across prefix overlaps (user "al" vs
    /// "ali" both matching "ali1…", Docs/22 D3).
    /// </summary>
    private static string BuildNewAccountAction(string vpn_username, int plan_id, PurchaseLimits limits,
        int realm_id, string owner_username, int? actor_id = null)
    {
        var parts = new List<string>
        {
            NewAccountActionPrefix.TrimEnd('|'),
            $"{vpn_username}t",
            $"{owner_username}o",
            $"{plan_id}p",
        };

        if (limits.Users.HasValue) parts.Add($"{limits.Users.Value}u");
        if (limits.Days.HasValue) parts.Add($"{limits.Days.Value}d");
        if (limits.Gigabytes.HasValue) parts.Add($"{limits.Gigabytes.Value}g");
        if (limits.RateLimitMeg.HasValue) parts.Add($"{limits.RateLimitMeg.Value}b");

        parts.Add($"{realm_id}r");

        if (actor_id.HasValue) parts.Add($"{actor_id.Value}a");

        return string.Join("|", parts);
    }

    /// <summary>
    /// D5: warnings for axes the current unlimited band shadows — the buyer
    /// sees them on the estimate and can back out before invoicing.
    /// </summary>
    private static List<string> BuildStackingWarnings(PlanStateEntity? current, PurchaseLimits limits)
    {
        var warnings = new List<string>();

        if (!HasCurrentBand(current)) return warnings;

        if (!current!.TimeLimitInDays.HasValue && limits.Days.HasValue)
        {
            warnings.Add("مدت اکانت شما در حال حاضر نامحدود است؛ مدت خریداری‌شده در این تمدید اعمال نمی‌شود.");
        }

        if (!current.TrafficLimit.HasValue && limits.Gigabytes.HasValue)
        {
            warnings.Add("حجم اکانت شما در حال حاضر نامحدود است؛ حجم خریداری‌شده در این تمدید اعمال نمی‌شود.");
        }

        return warnings;
    }

    /// <summary>
    /// The view emits an all-NULL row for an account without renewals — that
    /// is "no current band", not "unlimited band". A real band always carries
    /// at least one non-null axis (Docs/24 §6).
    /// </summary>
    private static bool HasCurrentBand(PlanStateEntity? state)
    {
        return state != null &&
               (state.TimeLimitInDays.HasValue || state.TrafficLimit.HasValue ||
                state.SimultaneousUser.HasValue || state.RateLimitInMeg.HasValue);
    }

    /// <summary>
    /// D5: expiry stacking — an unlimited side dominates; otherwise the new
    /// days extend the later of now and the running expiry (D16, Docs/17).
    /// </summary>
    private static DateTime? ComposeStackedExpiry(PlanStateEntity current, short? days)
    {
        if (!days.HasValue) return null;
        if (!current.TimeLimitInDays.HasValue) return null;

        return LaterOf(DateTime.Now, current.ExpirationDate).AddDays(days.Value);
    }

    private static DateTime LaterOf(DateTime now, DateTime? current_expiry)
    {
        return current_expiry.HasValue && current_expiry.Value > now ? current_expiry.Value : now;
    }

    /// <summary>
    /// D16 (Docs/17) + D5 (Docs/24): the traffic quota carried into the new
    /// renewal — the cumulative remaining of the current band (view
    /// TrafficLeft; a NULL on a limited band means "no usage recorded yet",
    /// i.e. the full quota) plus this renewal's share, clamped at zero for
    /// overdrawn plans. An unlimited side (current or new) makes the stack
    /// unlimited; an account with no band at all starts from its own share.
    /// </summary>
    private static long? StackRemainingTraffic(PlanStateEntity? current_state, long? new_share)
    {
        if (!HasCurrentBand(current_state))
        {
            return new_share;
        }

        var remaining = current_state!.TrafficLeft ?? current_state.TrafficLimit;

        return remaining.HasValue && new_share.HasValue
            ? Math.Max(0, remaining.Value) + new_share.Value
            : null;
    }
}
