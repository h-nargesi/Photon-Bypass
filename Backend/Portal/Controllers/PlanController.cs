using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Portal.Context;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

[Authorize]
[ApiController]
[Route("/api/[controller]")]
public class PlanController(
    IPlanApplication application,
    IPlanManagementApplication management,
    IJobContext job,
    Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private readonly IPlanApplication application = application;
    private readonly IPlanManagementApplication management = management;

    /// <summary>Selectable realms for the purchase flow (Docs/22 D4).</summary>
    [HttpGet("realms")]
    public async Task<ApiResult> GetRealms()
    {
        await LoadJobContextAsync();

        var result = await application.GetRealms();

        return SafeApiResult(result);
    }

    /// <summary>
    /// Purchasable plans of the acting pricing group (Docs/24 §5): axis
    /// pairs + realm choices. <c>?target=</c> serves the admin on-behalf view.
    /// </summary>
    [HttpGet("catalog")]
    public async Task<ApiResult> GetCatalog([FromQuery] string? target = null)
    {
        await LoadJobContextAsync(target);

        var result = await application.GetCatalog(target);

        return SafeApiResult(result);
    }

    /// <summary>D3 system suggestion for the next VPN username of the acting user.</summary>
    [HttpGet("suggest-username")]
    public async Task<ApiResult> SuggestUsername()
    {
        await LoadJobContextAsync();

        var result = await application.SuggestUsername();

        return SafeApiResult(result);
    }

    [HttpGet("plan-state")]
    public async Task<ApiResult> GetPlanState([FromQuery] int? accountId)
    {
        await LoadJobContextAsync(accountId);

        var result = await application.GetPlanState(accountId!.Value);

        return SafeApiResult(result);
    }

    [HttpGet("plan-info")]
    public async Task<ApiResult> GetPlanInfo([FromQuery] int? accountId)
    {
        await LoadJobContextAsync(accountId);

        var result = await application.GetPlanInfo(accountId!.Value);

        return SafeApiResult(result);
    }

    [HttpPost("estimate")]
    public async Task<ApiResult> Estimate([FromBody] RenewalContext context)
    {
        if (context.AccountId == null)
        {
            return BadRequestApiResult(message: "شناسه‌ی اکانت VPN ارسال نشده است!");
        }

        await LoadJobContextAsync(context.AccountId);

        if (context.PlanId == null)
        {
            return BadRequestApiResult(message: "پلن انتخاب نشده است!");
        }

        var result = await application.Estimate(context.AccountId!.Value, ToPurchaseRequest(context));

        return SafeApiResult(result);
    }

    [HttpPost("renewal")]
    public async Task<ApiResult> Renewal([FromBody] RenewalContext context)
    {
        if (context.AccountId == null)
        {
            return BadRequestApiResult(message: "شناسه‌ی اکانت VPN ارسال نشده است!");
        }

        await LoadJobContextAsync(context.AccountId);

        if (context.PlanId == null)
        {
            return BadRequestApiResult(message: "پلن انتخاب نشده است!");
        }

        var result = await application.Renewal(context.AccountId!.Value, ToPurchaseRequest(context));

        return SafeApiResult(result);
    }

    /// <summary>
    /// First purchase (Docs/22 D9): realm + VPN username + plan, settled
    /// through the same invoice flow as renewals.
    /// </summary>
    [HttpPost("new-account")]
    public async Task<ApiResult> NewAccount([FromBody] NewAccountContext context)
    {
        await LoadJobContextAsync(target: context.Target);

        if (context.RealmId == null)
        {
            return BadRequestApiResult(message: "انتخاب سرور الزامی است!");
        }

        if (context.PlanId == null)
        {
            return BadRequestApiResult(message: "پلن انتخاب نشده است!");
        }

        var result = await application.NewAccount(new NewAccountRequest
        {
            PlanId = context.PlanId.Value,
            SimultaneousUserCount = context.SimultaneousUserCount,
            Days = context.Days,
            Gigabytes = context.Gigabytes,
            RateLimitMeg = context.RateLimitMeg,
            RealmId = context.RealmId,
            Username = context.Username,
            Target = context.Target,
        });

        return SafeApiResult(result);
    }

    // ---- admin CRUD (Docs/24 D4/D5) --------------------------------------

    /// <summary>All plans (inactive included) with groups and realm sets.</summary>
    [HttpGet("list")]
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    public async Task<ApiResult> List()
    {
        await LoadJobContextAsync();

        var result = await management.List();

        return SafeApiResult(result);
    }

    [HttpPost("save")]
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    public async Task<ApiResult> Save([FromBody] SavePlanModel request)
    {
        await LoadJobContextAsync();

        var result = await management.Save(request);

        return SafeApiResult(result);
    }

    [HttpPost("delete")]
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    public async Task<ApiResult> Delete([FromBody] PlanDeleteRequest request)
    {
        await LoadJobContextAsync();

        var result = await management.Delete(request.Id);

        return SafeApiResult(result);
    }

    [HttpGet("group/list")]
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    public async Task<ApiResult> GroupList()
    {
        await LoadJobContextAsync();

        var result = await management.GroupList();

        return SafeApiResult(result);
    }

    [HttpPost("group/save")]
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    public async Task<ApiResult> SaveGroup([FromBody] SaveGroupModel request)
    {
        await LoadJobContextAsync();

        var result = await management.SaveGroup(request);

        return SafeApiResult(result);
    }

    [HttpPost("group/delete")]
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    public async Task<ApiResult> DeleteGroup([FromBody] GroupDeleteRequest request)
    {
        await LoadJobContextAsync();

        var result = await management.DeleteGroup(request.Id);

        return SafeApiResult(result);
    }

    /// <summary>
    /// D4: forced calculator pool rebuild — direct SQL edits raise no entity
    /// event, so out-of-band script changes need this explicit reload.
    /// </summary>
    [HttpPost("reload-calculators")]
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    public async Task<ApiResult> ReloadCalculators()
    {
        await LoadJobContextAsync();

        var result = await management.ReloadCalculators();

        return SafeApiResult(result);
    }

    private static PurchaseRequest ToPurchaseRequest(RenewalContext context)
    {
        return new PurchaseRequest
        {
            PlanId = context.PlanId!.Value,
            SimultaneousUserCount = context.SimultaneousUserCount,
            Days = context.Days,
            Gigabytes = context.Gigabytes,
            RateLimitMeg = context.RateLimitMeg,
            RealmId = context.RealmId,
        };
    }
}

public class PlanDeleteRequest
{
    public int Id { get; set; }
}

public class GroupDeleteRequest
{
    public int Id { get; set; }
}
