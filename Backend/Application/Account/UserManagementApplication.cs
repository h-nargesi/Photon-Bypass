using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Application.Account;

class UserManagementApplication(
    IAccountRepository account_repo,
    Lazy<IAccountApplication> account_app,
    Lazy<IHistoryRepository> history_repo,
    Lazy<ISelfServiceGuard> self_service_guard,
    Lazy<IAccessService> access,
    Lazy<IJobContext> job_context) : IUserManagementApplication
{
    private const int ListPageSize = 20;

    private const string ManagementHistoryTitle = "مدیریت";

    private IAccountRepository AccountRepo { get; } = account_repo;

    private Lazy<IAccountApplication> AccountApp { get; } = account_app;

    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;

    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;

    private Lazy<IAccessService> Access { get; } = access;

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

        var (rows, total) = await AccountRepo.SearchAccounts(owner_id, search, page, ListPageSize);

        var items = rows.Select(row => new UserRowModel
        {
            Username = row.Username,
            Name = row.Name,
            Surname = row.Surname,
            Email = row.Email,
            Role = row.Role,
            AllowSelfService = row.AllowSelfService,
            IsActive = row.IsActive,
            Owner = row.Owner,
            Created = row.Created,
            CreatedTitle = row.Created.ToPersianString(),
            SimultaneousUser = row.SimultaneousUser,
            ExpirationDate = row.ExpirationDate,
            TrafficLeft = row.TrafficLeft,
            LastConnectTime = row.LastConnectTime,
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
        var duplicated = await AccountRepo.CheckUniqueData(account.Username, account.Email, account.Mobile);

        // same contract as the unique-index catch in AccountRepository.Save
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

        account.VpnPassword = HashHandler.GenerateHashCode();
        account.Password = PasswordHasher.Hash(model.Password);

        await AccountRepo.Save(account);

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
            await AccountRepo.HasActiveSubordinate(account.Id))
        {
            throw new UserException("این مدیر زیر دست فعال دارد؛ ابتدا آنها را جدا یا غیرفعال کنید!",
                $"Cannot demote a manager with active subordinates (docs/20 §3): target={account.Username}");
        }

        account.Role = role;

        await AccountRepo.Save(account);

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

        await AccountRepo.Save(account);

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

        await AccountRepo.Save(account);

        await SaveHistory(account.Id, allow
            ? "دسترسی مالی (self-service) فعال شد."
            : "دسترسی مالی (self-service) غیرفعال شد.");

        Log.Information("Self-service flag changed by {0}: target={1}, allow={2}", actor.Username, account.Username, allow);

        return ApiResult.Success("وضعیت دسترسی مالی کاربر ذخیره شد.");
    }

    public async Task<ApiResult> SetActive(string target, bool active)
    {
        var actor = await ResolveActor();
        var account = await GetTarget(target);

        EnsureNotSelf(actor, account, "تغییر وضعیت خود از این مسیر مجاز نیست!");

        if (actor.Role == AccountRole.Admin)
        {
            // the admin may (de)activate anyone but itself — D9/D13
        }
        else if (actor.Role == AccountRole.Manager)
        {
            EnsureSubordinate(actor, account);
        }
        else
        {
            throw Forbidden("تغییر وضعیت کاربر مجاز نیست!", actor);
        }

        account.IsActive = active;

        await AccountRepo.Save(account);

        Access.Value.InvalidateTargetArea(account.Username);

        await SaveHistory(account.Id, active ? "حساب فعال شد." : "حساب غیرفعال شد.");

        Log.Information("Active flag changed by {0}: target={1}, active={2}", actor.Username, account.Username, active);

        return ApiResult.Success(active ? "حساب کاربر فعال شد." : "حساب کاربر غیرفعال شد.");
    }

    private async Task<AccountEntity> ResolveActor()
    {
        return await SelfServiceGuard.Value.ResolveActor();
    }

    private async Task<AccountEntity> RequireAdmin()
    {
        var actor = await ResolveActor();

        if (actor.Role != AccountRole.Admin)
        {
            throw Forbidden("این عملیات فقط برای ادمین مجاز است!", actor);
        }

        return actor;
    }

    private async Task<AccountEntity> GetTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new UserException("کاربر هدف مشخص نشده!", "Management endpoints require a target username.");
        }

        return await AccountRepo.GetAccount(target)
               ?? throw new UserException("کاربر پیدا نشد!", $"Account not found. target:{target}");
    }

    /// <summary>
    /// The attach target of <c>set-owner</c>/<c>create</c>: an active manager
    /// (docs/20 §3 — subordinates hang off managers), never the account being
    /// attached and never through an ownership cycle.
    /// </summary>
    private async Task<AccountEntity> ResolveManagerOwner(string owner_username, AccountEntity? exclude)
    {
        var owner = await AccountRepo.GetAccount(owner_username)
                    ?? throw new UserException("مدیر جدید پیدا نشد!", $"Owner account not found: {owner_username}");

        if (!owner.IsActive || owner.Role != AccountRole.Manager)
        {
            throw new UserException("مالک جدید باید یک مدیر فعال باشد!",
                $"Owner must be an active manager (docs/20 §3): {owner_username}, role={owner.Role}, active={owner.IsActive}");
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

                var next = await AccountRepo.GetActiveAccount(current.Owner.Value);

                if (next == null)
                {
                    break;
                }

                current = next;
            }
        }

        return owner;
    }

    private static void EnsureNotSelf(AccountEntity actor, AccountEntity account, string message)
    {
        if (actor.Id == account.Id)
        {
            throw new UserException(message,
                $"Self-operation refused (docs/20 D9): actor={actor.Username}", 403);
        }
    }

    private static void EnsureNotAdmin(AccountEntity account, string message)
    {
        if (account.Role == AccountRole.Admin)
        {
            throw new UserException(message,
                $"The single admin account is out of scope for management operations (docs/20 D9): {account.Username}");
        }
    }

    private static void EnsureSubordinate(AccountEntity actor, AccountEntity account)
    {
        if (account.Owner != actor.Id)
        {
            throw Forbidden("این کاربر زیر دست شما نیست!", actor);
        }
    }

    private static UserException Forbidden(string message, AccountEntity actor)
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

    private async Task InvalidateOwner(int owner_id)
    {
        var owner = await AccountRepo.GetActiveAccount(owner_id);

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
