using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Portal.Context;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

[Authorize]
[ApiController]
[Route("/api/[controller]")]
public class PlanController(
    IPlanApplication application, IJobContext job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private readonly IPlanApplication application = application;

    /// <summary>Selectable realms for the purchase flow (Docs/22 D4).</summary>
    [HttpGet("realms")]
    public async Task<ApiResult> GetRealms()
    {
        await LoadJobContextAsync();

        var result = await application.GetRealms();

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
        if (accountId == null)
        {
            return BadRequestApiResult(message: MissingAccountIdMessage);
        }

        await LoadJobContextAsync(accountId);

        var result = await application.GetPlanState(accountId!.Value);

        return SafeApiResult(result);
    }

    [HttpGet("plan-info")]
    public async Task<ApiResult> GetPlanInfo([FromQuery] int? accountId)
    {
        if (accountId == null)
        {
            return BadRequestApiResult(message: MissingAccountIdMessage);
        }

        await LoadJobContextAsync(accountId);

        var result = await application.GetPlanInfo(accountId!.Value);

        return SafeApiResult(result);
    }

    [HttpPost("estimate")]
    public async Task<ApiResult> Estimate([FromBody] RenewalContext context)
    {
        if (context.AccountId == null)
        {
            return BadRequestApiResult(message: MissingAccountIdMessage);
        }

        await LoadJobContextAsync(context.AccountId);

        if (!context.Days.HasValue && !context.Gigabytes.HasValue)
        {
            return BadRequestApiResult(message: "مشخصات پلن مشخص نشده است!");
        }

        if (!context.SimultaneousUserCount.HasValue)
        {
            return BadRequestApiResult(message: "تعداد کاربران مشخص نشده است!");
        }

        var result = await application.Estimate(context.AccountId!.Value,
            context.SimultaneousUserCount.Value, context.Days ?? 0, context.Gigabytes ?? 0);

        return SafeApiResult(result);
    }

    [HttpPost("renewal")]
    public async Task<ApiResult> Renewal([FromBody] RenewalContext context)
    {
        if (context.AccountId == null)
        {
            return BadRequestApiResult(message: MissingAccountIdMessage);
        }

        await LoadJobContextAsync(context.AccountId);

        if (!context.Days.HasValue && !context.Gigabytes.HasValue)
        {
            return BadRequestApiResult(message: "مشخصات پلن مشخص نشده است!");
        }

        if (!context.SimultaneousUserCount.HasValue)
        {
            return BadRequestApiResult(message: "تعداد کاربران مشخص نشده است!");
        }

        var result = await application.Renewal(context.AccountId!.Value,
            context.SimultaneousUserCount.Value, context.Days ?? 0, context.Gigabytes ?? 0, context.RealmId);

        return SafeApiResult(result);
    }

    /// <summary>
    /// First purchase (Docs/22 D9): realm + VPN username + plan, settled
    /// through the same invoice flow as renewals.
    /// </summary>
    [HttpPost("new-account")]
    public async Task<ApiResult> NewAccount([FromBody] NewAccountContext context)
    {
        await LoadJobContextAsync();

        if (!context.RealmId.HasValue)
        {
            return BadRequestApiResult(message: "انتخاب سرور الزامی است!");
        }

        if (!context.Days.HasValue && !context.Gigabytes.HasValue)
        {
            return BadRequestApiResult(message: "مشخصات پلن مشخص نشده است!");
        }

        if (!context.SimultaneousUserCount.HasValue)
        {
            return BadRequestApiResult(message: "تعداد کاربران مشخص نشده است!");
        }

        var result = await application.NewAccount(context.Username, context.RealmId.Value,
            context.SimultaneousUserCount.Value, context.Days ?? 0, context.Gigabytes ?? 0, context.Target);

        return SafeApiResult(result);
    }
}
