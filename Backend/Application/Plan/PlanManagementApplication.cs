using PhotonBypass.Application.Account;
using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanManagementApplication(
    Lazy<IPlanRepository> plan_repo,
    Lazy<IUserGroupRepository> group_repo,
    Lazy<IRealmRepository> realm_repo,
    Lazy<IServerRepository> server_repo,
    Lazy<ISelfServiceGuard> self_service_guard,
    IPriceCalculator price_calc) : IPlanManagementApplication
{
    private Lazy<IPlanRepository> PlanRepo { get; } = plan_repo;
    private Lazy<IUserGroupRepository> GroupRepo { get; } = group_repo;
    private Lazy<IRealmRepository> RealmRepo { get; } = realm_repo;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;
    private IPriceCalculator PriceCalc { get; } = price_calc;

    public async Task<ApiResult<List<PlanRowModel>>> List()
    {
        await RequireAdmin();

        var groups = (await GroupRepo.Value.GetAll()).ToDictionary(group => group.Id);
        var plans = await PlanRepo.Value.GetAll();

        var result = new List<PlanRowModel>();

        foreach (var plan in plans)
        {
            result.Add(new PlanRowModel
            {
                Id = plan.Id,
                Name = plan.Name,
                Title = plan.Title,
                Caption = plan.Caption,
                Description = plan.Description,
                ShowOnWebsite = plan.ShowOnWebsite,
                IsActive = plan.IsActive,
                GroupId = plan.GroupId,
                GroupTitle = groups.GetValueOrDefault(plan.GroupId)?.Title ?? $"#{plan.GroupId}",
                UsersBase = plan.UsersBase,
                UsersCap = plan.UsersCap,
                DaysBase = plan.DaysBase,
                DaysCap = plan.DaysCap,
                TrafficBase = plan.TrafficBase,
                TrafficCap = plan.TrafficCap,
                RateBase = plan.RateBase,
                RateCap = plan.RateCap,
                CalculatorCode = plan.CalculatorCode,
                RealmIds = await PlanRepo.Value.GetRealmIds(plan.Id),
            });
        }

        return ApiResult<List<PlanRowModel>>.Success(result);
    }

    public async Task<ApiResult> Save(SavePlanModel model)
    {
        await RequireAdmin();

        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Title))
        {
            throw new UserException("نام و عنوان پلن الزامی است!", $"Plan name/title missing: name={model.Name}, title={model.Title}");
        }

        if (string.IsNullOrWhiteSpace(model.CalculatorCode))
        {
            throw new UserException("کد محاسبه‌گر پلن الزامی است!", "Plan calculator code missing.");
        }

        // fail fast: the snippet must compile against the closed contract
        // (D2) before anything is persisted
        PriceCalc.ValidateCode(model.CalculatorCode);

        var plan = model.Id.HasValue
            ? await PlanRepo.Value.Find(model.Id.Value)
              ?? throw new UserException("پلن مورد نظر پیدا نشد!", $"Plan not found: id={model.Id}")
            : new PlanEntity();

        plan.Name = model.Name.Trim();
        plan.Title = model.Title.Trim();
        plan.Caption = string.IsNullOrWhiteSpace(model.Caption) ? null : model.Caption.Trim();
        plan.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        plan.ShowOnWebsite = model.ShowOnWebsite;
        plan.IsActive = model.IsActive;
        plan.GroupId = model.GroupId;
        plan.UsersBase = model.UsersBase;
        plan.UsersCap = model.UsersCap;
        plan.DaysBase = model.DaysBase;
        plan.DaysCap = model.DaysCap;
        plan.TrafficBase = model.TrafficBase;
        plan.TrafficCap = model.TrafficCap;
        plan.RateBase = model.RateBase;
        plan.RateCap = model.RateCap;
        plan.CalculatorCode = model.CalculatorCode;

        if (plan.ValidateDefinition(out var definition_exception))
        {
            throw definition_exception;
        }

        if (await GroupRepo.Value.Find(plan.GroupId) == null)
        {
            throw new UserException("گروه کاربری انتخابی پیدا نشد!", $"Plan group not found: id={plan.GroupId}");
        }

        // duplicate-name guard (the unique index stays as the TOCTOU backstop)
        var same_name = await PlanRepo.Value.FindByName(plan.Name);
        if (same_name != null && same_name.Id != plan.Id)
        {
            throw new UserException("این نام پلن قبلا استفاده شده است!",
                $"Duplicate plan name: {plan.Name}", 409);
        }

        var realm_ids = model.RealmIds.Distinct().ToList();

        if (realm_ids.Count > 0)
        {
            var found_realms = await RealmRepo.Value.GetByIds(realm_ids);

            if (found_realms.Count != realm_ids.Count)
            {
                throw new UserException("یکی از منطقه‌های انتخابی پلن پیدا نشد!",
                    $"Plan realm set contains unknown realms: requested={realm_ids}, found={found_realms.Keys}");
            }
        }

        await EnsureRateLimitEnforceable(realm_ids, plan.RateBase.HasValue);

        await PlanRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            await PlanRepo.Value.Save(plan);

            await PlanRepo.Value.SetRealmIds(plan.Id, realm_ids);

            await PlanRepo.Value.DbContext.CommitAsync();
        }
        catch
        {
            await SafeRollback();
            throw;
        }

        Log.Information("Plan saved by admin: id={0}, name={1}, group={2}, realms=[{3}]",
            plan.Id, plan.Name, plan.GroupId, string.Join(",", realm_ids));

        return ApiResult.Success("پلن ذخیره شد.");
    }

    public async Task<ApiResult> Delete(int plan_id)
    {
        await RequireAdmin();

        if (await PlanRepo.Value.IsReferencedByRenewals(plan_id))
        {
            throw new UserException("این پلن در تمدیدهای ثبت‌شده استفاده شده و قابل حذف نیست!",
                $"Plan still referenced by renewals: id={plan_id}");
        }

        var plan = await PlanRepo.Value.Find(plan_id)
                   ?? throw new UserException("پلن مورد نظر پیدا نشد!", $"Plan not found: id={plan_id}");

        await PlanRepo.Value.Delete(plan);

        Log.Information("Plan deleted by admin: id={0}, name={1}", plan.Id, plan.Name);

        return ApiResult.Success("پلن حذف شد.");
    }

    public async Task<ApiResult<List<GroupRowModel>>> GroupList()
    {
        await RequireAdmin();

        var member_counts = await GroupRepo.Value.GetMemberCounts();

        var result = (await GroupRepo.Value.GetAll())
            .Select(group => new GroupRowModel
            {
                Id = group.Id,
                Title = group.Title,
                IsDefault = group.IsDefault,
                MemberCount = member_counts.GetValueOrDefault(group.Id),
            })
            .ToList();

        return ApiResult<List<GroupRowModel>>.Success(result);
    }

    public async Task<ApiResult> SaveGroup(SaveGroupModel model)
    {
        await RequireAdmin();

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            throw new UserException("عنوان گروه الزامی است!", "Group title missing.");
        }

        var group = model.Id.HasValue
            ? await GroupRepo.Value.Find(model.Id.Value)
              ?? throw new UserException("گروه مورد نظر پیدا نشد!", $"Group not found: id={model.Id}")
            : new UserGroupEntity();

        if (group.IsDefault && !model.IsDefault)
        {
            throw new UserException("گروه پیش‌فرض قابل تغییر به غیرپیش‌فرض نیست!",
                $"Removing the default flag is refused — exactly one default group must exist (Docs/24 D6): id={group.Id}");
        }

        group.Title = model.Title.Trim();
        group.IsDefault = model.IsDefault;

        // duplicate-title guard (the unique index stays as the TOCTOU backstop)
        var same_title = await GroupRepo.Value.FindByTitle(group.Title);
        if (same_title != null && same_title.Id != group.Id)
        {
            throw new UserException("این عنوان گروه قبلا استفاده شده است!",
                $"Duplicate group title: {group.Title}", 409);
        }

        await GroupRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            if (group.IsDefault)
            {
                await GroupRepo.Value.ClearDefault(group.Id);
            }

            await GroupRepo.Value.Save(group);

            await GroupRepo.Value.DbContext.CommitAsync();
        }
        catch
        {
            await SafeGroupRollback();
            throw;
        }

        Log.Information("User group saved by admin: id={0}, title={1}, is-default={2}", group.Id, group.Title, group.IsDefault);

        return ApiResult.Success("گروه ذخیره شد.");
    }

    public async Task<ApiResult> DeleteGroup(int group_id)
    {
        await RequireAdmin();

        var group = await GroupRepo.Value.Find(group_id)
                    ?? throw new UserException("گروه مورد نظر پیدا نشد!", $"Group not found: id={group_id}");

        if (group.IsDefault)
        {
            throw new UserException("گروه پیش‌فرض قابل حذف نیست!",
                $"The default group cannot be deleted (Docs/24 D6): id={group_id}");
        }

        if (await GroupRepo.Value.HasMembers(group_id))
        {
            throw new UserException("این گروه هنوز عضو دارد؛ ابتدا اعضا را منتقل کنید!",
                $"Group with members cannot be deleted: id={group_id}");
        }

        // plans of the group block the FK — refuse with a distinct message
        var group_plans = await PlanRepo.Value.GetActiveByGroup(group_id);

        if (group_plans.Count > 0 || await HasAnyPlan(group_id))
        {
            throw new UserException("این گروه هنوز پلن دارد؛ ابتدا پلن‌ها را حذف یا منتقل کنید!",
                $"Group with plans cannot be deleted: id={group_id}");
        }

        await GroupRepo.Value.Delete(group);

        Log.Information("User group deleted by admin: id={0}, title={1}", group.Id, group.Title);

        return ApiResult.Success("گروه حذف شد.");
    }

    public async Task<ApiResult> ReloadCalculators()
    {
        await RequireAdmin();

        await PriceCalc.Reload();

        Log.Information("Calculator pool reloaded by admin (Docs/24 D4).");

        return ApiResult.Success("محاسبه‌گرها بازآوری شدند.");
    }

    /// <summary>
    /// D10: the x-ui API has no per-client bandwidth limit, so a rate-limited
    /// plan may not offer realms served by x-ui data-planes — refused loudly
    /// at save time (and re-checked at purchase against the live realm set).
    /// </summary>
    private async Task EnsureRateLimitEnforceable(List<int> realm_ids, bool has_rate_axis)
    {
        if (!has_rate_axis || realm_ids.Count == 0) return;

        var clusters = await ServerRepo.Value.GetAllActiveNasInRealm(realm_ids);

        foreach (var (realm_id, servers) in clusters)
        {
            if (servers.Any(server => (server.Features & ServerFeature.Xui) != 0))
            {
                throw new UserException(
                    $"منطقه «{realm_id}» توسط سرور x-ui ارائه می‌شود و پهنای باند را پشتیبانی نمی‌کند!",
                    $"Rate-limited plan on an x-ui realm refused (Docs/24 D10): realm-id={realm_id}");
            }
        }
    }

    private async Task<bool> HasAnyPlan(int group_id)
    {
        return (await PlanRepo.Value.GetAll()).Any(plan => plan.GroupId == group_id);
    }

    private async Task RequireAdmin()
    {
        var actor = await SelfServiceGuard.Value.ResolveActor();

        if (actor.Role != AccountRole.Admin)
        {
            throw new UserException("این عملیات فقط برای ادمین مجاز است!",
                $"Plan management refused (Docs/24 §5): actor={actor.Username}, role={actor.Role}", 403);
        }
    }

    private async Task SafeRollback()
    {
        try
        {
            await PlanRepo.Value.DbContext.RollbackAsync();
        }
        catch (Exception rollback_error)
        {
            Log.Error(rollback_error, "Plan save rollback failed.");
        }
    }

    private async Task SafeGroupRollback()
    {
        try
        {
            await GroupRepo.Value.DbContext.RollbackAsync();
        }
        catch (Exception rollback_error)
        {
            Log.Error(rollback_error, "Group save rollback failed.");
        }
    }
}
