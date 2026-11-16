using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Servers;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Account;

class UserManagementApplication(
    IPanelUserRepository panel_user_repo,
    Lazy<IVpnAccountRepository> vpn_account_repo,
    Lazy<IRealmRepository> realm_repo,
    Lazy<IPlanStateRepository> plan_state_repo,
    Lazy<IAccountApplication> account_app,
    Lazy<IHistoryRepository> history_repo,
    Lazy<ISelfServiceGuard> self_service_guard,
    Lazy<IAccessService> access,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IJobContext> job_context) : IUserManagementApplication
{
    private const int ListPageSize = 20;

    private const string ManagementHistoryTitle = "مدیریت";

    private IPanelUserRepository PanelUserRepo { get; } = panel_user_repo;

    private Lazy<IVpnAccountRepository> VpnAccountRepo { get; } = vpn_account_repo;

    private Lazy<IRealmRepository> RealmRepo { get; } = realm_repo;

    private Lazy<IPlanStateRepository> PlanStateRepo { get; } = plan_state_repo;

    private Lazy<IAccountApplication> AccountApp { get; } = account_app;

    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;

    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;

    private Lazy<IAccessService> Access { get; } = access;

    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;

    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult<PagedResult<UserRowModel>>> GetList(string? search, int page)
    {
        if (page < 1)
        {
            throw new UserException("شماره صفحه نامعتبر است!", $"Invalid user list page: {page}");
        }

        var actor = await ResolveActor();

        if (actor.Role == AccountRole.User)
        {
            throw Forbidden("مشاهده لیست کاربران مجاز نیست!", actor);
        }

        // manager → own subordinates (inactive included); admin → everyone
        int? owner_id = actor.Role == AccountRole.Admin ? null : actor.Id;

        var (rows, total) = await PanelUserRepo.SearchUsers(owner_id, search, page, ListPageSize);

        // docs/22 §3.6: the VPN accounts nest under their user rows — one
        // paged query, never per-row
        var accounts = await VpnAccountRepo.Value.GetAccountsOverview(rows.Select(row => row.Username));

        var items = rows.Select(row => new UserRowModel
        {
            Username = row.Username,
            Name = row.Name,
            Surname = row.Surname,
            Email = row.Email,
            Role = row.Role,
            AllowSelfService = row.AllowSelfService,
            Status = row.Status,
            Owner = row.Owner,
            Created = row.Created,
            CreatedTitle = row.Created.ToPersianString(),
            Accounts = accounts.Where(account => account.Owner == row.Username)
                .Select(account => new UserAccountRowModel
                {
                    Id = account.AccountId,
                    Username = account.Username,
                    RealmId = account.RealmId,
                    RealmName = account.RealmName,
                    Status = account.Status,
                    Created = account.Created,
                    SimultaneousUser = account.SimultaneousUser,
                    ExpirationDate = account.ExpirationDate,
                    TrafficLeft = account.TrafficLeft,
                    LastConnectTime = account.LastConnectTime,
                })
                .ToList(),
        });

        return ApiResult<PagedResult<UserRowModel>>.Success(new PagedResult<UserRowModel>
        {
            Page = page,
            PageSize = ListPageSize,
            TotalCount = total,
            Items = [.. items],
        });
    }

    public async Task<ApiResult> Create(CreateUserModel model)
    {
        // same policy as self-service registration (docs/20 D12: the creator
        // sets the initial password — it must satisfy the portal policy)
        AccountBusiness.ValidatePassword(model.Password);

        var actor = await ResolveActor();

        var account = AccountBusiness.CreateFromModel(model);
        var duplicated = await PanelUserRepo.CheckUniqueData(account.Username, account.Email, account.Mobile);

        // same contract as the unique-index catch in PanelUserRepository.Save
        // (docs/20 step 17), with friendlier per-field messages
        if ((duplicated & 1) == 1)
        {
            throw new UserException("این نام کاربری قبلا استفاده شده است!", $"Duplicate username: {account.Username}", 409);
        }

        if ((duplicated & 2) == 2)
        {
            throw new UserException("این ایمیل قبلا استفاده شده است!", $"Duplicate email: {account.Email}", 409);
        }

        if ((duplicated & 4) == 4)
        {
            throw new UserException("این  شماره همراه قبلا استفاده شده است!", $"Duplicate mobile: {account.Mobile}", 409);
        }

        if (actor.Role == AccountRole.Manager)
        {
            if (model.Role is not (null or AccountRole.User) || !string.IsNullOrWhiteSpace(model.Owner))
            {
                throw new UserException("تعیین نقش یا مالک فقط برای ادمین مجاز است!",
                    $"Manager tried to create with role/owner: role={model.Role}, owner={model.Owner}, actor={actor.Username}");
            }

            account.Owner = actor.Id;
        }
        else
        {
            if (model.Role == AccountRole.Admin)
            {
                throw new UserException("ساخت حساب ادمین مجاز نیست!",
                    "Admin creation is refused (docs/20 D9: exactly one admin, no promotion path).");
            }

            account.Role = model.Role ?? AccountRole.User;

            if (!string.IsNullOrWhiteSpace(model.Owner))
            {
                account.Owner = (await ResolveManagerOwner(model.Owner, exclude: null)).Id;
            }
        }

        if (model.AllowSelfService)
        {
            account.UserType |= UserTypes.AllowSelfService;
        }

        // docs/22 D9: a panel user is created bare — its VPN account (and
        // credentials) arrive with the first purchase
        account.Password = PasswordHasher.Hash(model.Password);

        await PanelUserRepo.Save(account);

        await SaveHistory(account.Id, "کاربر جدید ساخته شد.");

        Log.Information("User created by {0}: username={1}, owner={2}, role={3}, self-service={4}",
            actor.Username, account.Username, account.Owner, account.Role, model.AllowSelfService);

        return ApiResult.Success("کاربر ساخته شد.");
    }

    public async Task<ApiResult> SetPassword(string target, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new UserException("کلمه عبور خالی است!");
        }

        var actor = await ResolveActor();
        var account = await GetTarget(target);

        if (actor.Role == AccountRole.Admin)
        {
            EnsureNotSelf(actor, account, "تغییر کلمه عبور خود از این مسیر مجاز نیست!");
        }
        else if (actor.Role == AccountRole.Manager)
        {
            EnsureSubordinate(actor, account);
        }
        else
        {
            throw Forbidden("بازنشانی کلمه عبور مجاز نیست!", actor);
        }

        return await AccountApp.Value.ChangePassword(account, password);
    }

    public async Task<ApiResult> SetRole(string target, AccountRole role)
    {
        var actor = await RequireAdmin();

        if (!Enum.IsDefined(role) || role == AccountRole.Admin)
        {
            throw new UserException("این نقش قابل اعمال نیست!",
                $"Role must be User or Manager — Admin has no promotion path (docs/20 D9): {role}");
        }

        var account = await GetTarget(target);

        EnsureNotSelf(actor, account, "تغییر نقش خود مجاز نیست!");
        EnsureNotAdmin(account, "نقش حساب ادمین قابل تغییر نیست!");

        if (account.Role == AccountRole.Manager && role == AccountRole.User &&
            await PanelUserRepo.HasActiveSubordinate(account.Id))
        {
            throw new UserException("این مدیر زیر دست فعال دارد؛ ابتدا آنها را جدا یا غیرفعال کنید!",
                $"Cannot demote a manager with active subordinates (docs/20 §3): target={account.Username}");
        }

        account.Role = role;

        await PanelUserRepo.Save(account);

        // the cached target area carries the role — drop it so the next
        // login/checks pick the new one up immediately
        Access.Value.InvalidateTargetArea(account.Username);

        await SaveHistory(account.Id, $"نقش به «{PersianRole(role)}» تغییر کرد.");

        Log.Information("Role changed by {0}: target={1}, role={2}", actor.Username, account.Username, role);

        return ApiResult.Success("نقش کاربر تغییر کرد.");
    }

    public async Task<ApiResult> SetOwner(string target, string? owner)
    {
        var actor = await RequireAdmin();

        var account = await GetTarget(target);

        EnsureNotSelf(actor, account, "تغییر مالک خود مجاز نیست!");
        EnsureNotAdmin(account, "حساب ادمین زیر دست هیچ‌کس نیست!");

        var old_owner_id = account.Owner;
        var new_owner = string.IsNullOrWhiteSpace(owner) ? null : await ResolveManagerOwner(owner, exclude: account);

        account.Owner = new_owner?.Id;

        await PanelUserRepo.Save(account);

        // both target areas changed shape: the detached and the attached manager
        if (old_owner_id.HasValue)
        {
            await InvalidateOwner(old_owner_id.Value);
        }

        if (new_owner != null)
        {
            Access.Value.InvalidateTargetArea(new_owner.Username);
        }

        await SaveHistory(account.Id, new_owner == null
            ? "از زیر دست جدا شد."
            : $"زیر دست «{new_owner.Username}» شد.");

        Log.Information("Owner changed by {0}: target={1}, owner={2}", actor.Username, account.Username, new_owner?.Username);

        return ApiResult.Success(new_owner == null ? "کاربر از زیر دست جدا شد." : "کاربر به مدیر جدید منتقل شد.");
    }

    public async Task<ApiResult> SetSelfService(string target, bool allow)
    {
        var actor = await ResolveActor();
        var account = await GetTarget(target);

        if (actor.Role == AccountRole.Admin)
        {
            // the admin may flip anyone (docs/20 §3)
        }
        else if (actor.Role == AccountRole.Manager)
        {
            EnsureSubordinate(actor, account);
        }
        else
        {
            throw Forbidden("تغییر وضعیت دسترسی مالی مجاز نیست!", actor);
        }

        if (allow)
        {
            account.UserType |= UserTypes.AllowSelfService;
        }
        else
        {
            account.UserType &= ~UserTypes.AllowSelfService;
        }

        await PanelUserRepo.Save(account);

        await SaveHistory(account.Id, allow
            ? "دسترسی مالی (self-service) فعال شد."
            : "دسترسی مالی (self-service) غیرفعال شد.");

        Log.Information("Self-service flag changed by {0}: target={1}, allow={2}", actor.Username, account.Username, allow);

        return ApiResult.Success("وضعیت دسترسی مالی کاربر ذخیره شد.");
    }

    public async Task<ApiResult> SetStatus(string target, PanelUserStatus status)
    {
        var actor = await ResolveActor();
        var account = await GetTarget(target);

        EnsureNotSelf(actor, account, "تغییر وضعیت خود از این مسیر مجاز نیست!");

        if (actor.Role == AccountRole.Admin)
        {
            // the admin may change anyone's status but itself — D9/D13
        }
        else if (actor.Role == AccountRole.Manager)
        {
            EnsureSubordinate(actor, account);
        }
        else
        {
            throw Forbidden("تغییر وضعیت کاربر مجاز نیست!", actor);
        }

        if (!Enum.IsDefined(status))
        {
            throw new UserException("این وضعیت قابل اعمال نیست!",
                $"Undefined panel-user status (docs/22 D12): {status}");
        }

        if (account.Status == status)
        {
            return ApiResult.Success("وضعیت کاربر تغییر نکرد.");
        }

        var was_deactivated = account.Status == PanelUserStatus.Deactivated;

        account.Status = status;

        await PanelUserRepo.Save(account);

        // docs/22 D12: panel deactivation carries the VPN accounts with it
        // (old whole-account semantics), but a manager-suspended account
        // keeps its suspension across a later reactivation (docs/20 D13)
        if (status == PanelUserStatus.Deactivated)
        {
            await VpnAccountRepo.Value.SetStatus(
                (await VpnAccountRepo.Value.GetByUser(account.Id)).Select(vpn_account => vpn_account.Id),
                VpnAccountStatus.Deactivated);
        }
        else if (was_deactivated)
        {
            var vpn_accounts = await VpnAccountRepo.Value.GetByUser(account.Id);

            await VpnAccountRepo.Value.SetStatus(
                vpn_accounts.Where(vpn_account => vpn_account.Status == VpnAccountStatus.Deactivated)
                    .Select(vpn_account => vpn_account.Id),
                VpnAccountStatus.Active);
        }

        // the cached target area only depends on the Deactivated flag — drop
        // it on transitions crossing that boundary so access rules apply now
        if (was_deactivated || status == PanelUserStatus.Deactivated)
        {
            Access.Value.InvalidateTargetArea(account.Username);
        }

        await SaveHistory(account.Id, PersianStatusHistory(status));

        Log.Information("Panel-user status changed by {0}: target={1}, status={2}", actor.Username, account.Username, status);

        // best-effort immediate VPN sync of every account the user owns:
        // adapters skip clients already in the desired state, and the hourly
        // sweep reconciles any failure — closing live sessions is the radius
        // server's own job (D-D)
        await SyncAccountStatuses(account, status == PanelUserStatus.Active);

        return ApiResult.Success(PersianStatusMessage(status));
    }

    public async Task<ApiResult> SetAccountStatus(int account_id, VpnAccountStatus status)
    {
        var actor = await RequireManager();

        if (!Enum.IsDefined(status))
        {
            throw new UserException("این وضعیت قابل اعمال نیست!",
                $"Undefined VPN-account status (docs/22 D12): {status}");
        }

        var account = await VpnAccountRepo.Value.GetAccount(account_id)
                      ?? throw new UserException("اکانت VPN پیدا نشد!", $"VPN account not found: id={account_id}");

        if (actor.Role != AccountRole.Admin)
        {
            var owner = await PanelUserRepo.GetActiveUser(account.UserId);

            if (owner is not { Owner: int owner_id } || owner_id != actor.Id)
            {
                throw Forbidden("این اکانت VPN متعلق به زیرمجموعهی شما نیست!", actor);
            }
        }

        if (account.Status == status)
        {
            return ApiResult.Success("وضعیت اکانت تغییر نکرد.");
        }

        account.Status = status;

        await VpnAccountRepo.Value.Save(account);

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.UserId,
            VpnAccountId = account.Id,
            Category = EventCategory.Action,
            Type = EventType.Information,
            Title = ManagementHistoryTitle,
            Description = PersianAccountStatusHistory(status),
        });

        Log.Information("VPN-account status changed by {0}: account-id={1}, username={2}, status={3}",
            actor.Username, account.Id, account.Username, status);

        // best-effort immediate VPN sync; the hourly sweep reconciles (D-D)
        try
        {
            if (status == VpnAccountStatus.Active)
            {
                await AccountRadiusSrv.Value.ActivateUsers([account.Username]);
            }
            else
            {
                await AccountRadiusSrv.Value.DeactivateUsers([account.Username]);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[set-account-status] Immediate radius sync failed; the hourly sweep will reconcile: account-id={0}, status={1}",
                account.Id, status);
        }

        return ApiResult.Success(PersianAccountStatusMessage(status));
    }

    private async Task SyncAccountStatuses(PanelUserEntity account, bool activate)
    {
        try
        {
            var vpn_accounts = await VpnAccountRepo.Value.GetByUser(account.Id);

            if (vpn_accounts.Count == 0)
            {
                return;
            }

            var usernames = vpn_accounts.Select(vpn_account => vpn_account.Username).ToList();

            if (activate)
            {
                await AccountRadiusSrv.Value.ActivateUsers(usernames);
            }
            else
            {
                await AccountRadiusSrv.Value.DeactivateUsers(usernames);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[set-status] Immediate radius sync failed; the hourly sweep will reconcile: target={0}",
                account.Username);
        }
    }

    private async Task<PanelUserEntity> ResolveActor()
    {
        return await SelfServiceGuard.Value.ResolveActor();
    }

    private async Task<PanelUserEntity> RequireAdmin()
    {
        var actor = await ResolveActor();

        if (actor.Role != AccountRole.Admin)
        {
            throw Forbidden("این عملیات فقط برای ادمین مجاز است!", actor);
        }

        return actor;
    }

    private async Task<PanelUserEntity> RequireManager()
    {
        var actor = await ResolveActor();

        if (actor.Role == AccountRole.User)
        {
            throw Forbidden("این عملیات فقط برای مدیر و ادمین مجاز است!", actor);
        }

        return actor;
    }

    private async Task<PanelUserEntity> GetTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new UserException("کاربر هدف مشخص نشده!", "Management endpoints require a target username.");
        }

        return await PanelUserRepo.GetUser(target)
               ?? throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{target}");
    }

    /// <summary>
    /// The attach target of <c>set-owner</c>/<c>create</c>: an active manager
    /// (docs/20 §3 — subordinates hang off managers), never the account being
    /// attached and never through an ownership cycle.
    /// </summary>
    private async Task<PanelUserEntity> ResolveManagerOwner(string owner_username, PanelUserEntity? exclude)
    {
        var owner = await PanelUserRepo.GetUser(owner_username)
                    ?? throw new UserException("مدیر جدید پیدا نشد!", $"Owner account not found: {owner_username}");

        if (owner.Status == PanelUserStatus.Deactivated || owner.Role != AccountRole.Manager)
        {
            throw new UserException("مالک جدید باید یک مدیر فعال باشد!",
                $"Owner must be an active manager (docs/20 §3): {owner_username}, role={owner.Role}, status={owner.Status}");
        }

        if (exclude != null)
        {
            if (owner.Id == exclude.Id)
            {
                throw new UserException("کاربر نمی‌تواند زیر دست خودش باشد!",
                    $"Self-ownership refused: {owner_username}");
            }

            // walk the owner chain upwards: attaching under a descendant would
            // create a cycle (A→B→…→A)
            var current = owner;

            while (current.Owner.HasValue)
            {
                if (current.Owner.Value == exclude.Id)
                {
                    throw new UserException("این انتقال یک چرخه می‌سازد!",
                        $"Ownership cycle refused: {owner_username} is already under {exclude.Username}");
                }

                var next = await PanelUserRepo.GetActiveUser(current.Owner.Value);

                if (next == null)
                {
                    break;
                }

                current = next;
            }
        }

        return owner;
    }

    private static void EnsureNotSelf(PanelUserEntity actor, PanelUserEntity account, string message)
    {
        if (actor.Id == account.Id)
        {
            throw new UserException(message,
                $"Self-operation refused (docs/20 D9): actor={actor.Username}", 403);
        }
    }

    private static void EnsureNotAdmin(PanelUserEntity account, string message)
    {
        if (account.Role == AccountRole.Admin)
        {
            throw new UserException(message,
                $"The single admin account is out of scope for management operations (docs/20 D9): {account.Username}");
        }
    }

    private static void EnsureSubordinate(PanelUserEntity actor, PanelUserEntity account)
    {
        if (account.Owner != actor.Id)
        {
            throw Forbidden("این کاربر زیر دست شما نیست!", actor);
        }
    }

    private static UserException Forbidden(string message, PanelUserEntity actor)
    {
        return new UserException(message,
            $"Management operation refused (docs/20 §3): actor={actor.Username}, role={actor.Role}", 403);
    }

    private static string PersianRole(AccountRole role)
    {
        return role switch
        {
            AccountRole.Manager => "مدیر",
            _ => "کاربر",
        };
    }

    private static string PersianStatusHistory(PanelUserStatus status)
    {
        return status switch
        {
            PanelUserStatus.Active => "حساب کاربر فعال شد.",
            _ => "حساب کاربر غیرفعال شد.",
        };
    }

    private static string PersianStatusMessage(PanelUserStatus status)
    {
        return status switch
        {
            PanelUserStatus.Active => "حساب کاربر فعال شد.",
            _ => "حساب کاربر غیرفعال شد.",
        };
    }

    private static string PersianAccountStatusHistory(VpnAccountStatus status)
    {
        return status switch
        {
            VpnAccountStatus.Active => "اکانت VPN فعال شد.",
            VpnAccountStatus.Suspended => "سرویس VPN اکانت غیرفعال شد.",
            _ => "اکانت VPN غیرفعال شد.",
        };
    }

    private static string PersianAccountStatusMessage(VpnAccountStatus status)
    {
        return status switch
        {
            VpnAccountStatus.Active => "اکانت VPN فعال شد.",
            VpnAccountStatus.Suspended => "سرویس VPN اکانت غیرفعال شد.",
            _ => "اکانت VPN غیرفعال شد.",
        };
    }

    private async Task InvalidateOwner(int owner_id)
    {
        var owner = await PanelUserRepo.GetActiveUser(owner_id);

        if (owner != null)
        {
            Access.Value.InvalidateTargetArea(owner.Username);
        }
    }

    private Task SaveHistory(int target_id, string description)
    {
        return HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = target_id,
            Category = EventCategory.Action,
            Type = EventType.Information,
            Title = ManagementHistoryTitle,
            Description = description,
        });
    }
}
