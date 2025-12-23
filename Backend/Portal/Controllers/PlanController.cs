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

    [HttpGet("plan-state")]
    public async Task<ApiResult> GetPlanState([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.GetPlanState(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpGet("plan-info")]
    public async Task<ApiResult> GetPlanInfo([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.GetPlanInfo(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpPost("estimate")]
    public async Task<ApiResult> Estimate([FromBody] RenewalContext context)
    {
        LoadJobContext(context.Target);

        if (!context.Days.HasValue)
        {
            return BadRequestApiResult(message: "زمان پلن مشخص نشده است!");
        }

        if (!context.Gigabytes.HasValue)
        {
            return BadRequestApiResult(message: "حجم درخواست پلن مشخص نشده است!");
        }

        if (!context.SimultaneousUserCount.HasValue)
        {
            return BadRequestApiResult(message: "تعداد کاربران مشخص نشده است!");
        }

        var result = await application.Estimate(JobContext.Target, 
            context.SimultaneousUserCount.Value, context.Days.Value, context.Gigabytes.Value);

        return SafeApiResult(result);
    }

    [HttpPost("renewal")]
    public async Task<ApiResult> Renewal([FromBody] RenewalContext context)
    {
        LoadJobContext(context.Target);
        context.Target = JobContext.Target;

        if (!context.Days.HasValue)
        {
            return BadRequestApiResult(message: "زمان پلن مشخص نشده است!");
        }

        if (!context.Gigabytes.HasValue)
        {
            return BadRequestApiResult(message: "حجم درخواست پلن مشخص نشده است!");
        }

        if (!context.SimultaneousUserCount.HasValue)
        {
            return BadRequestApiResult(message: "تعداد کاربران مشخص نشده است!");
        }

        var result = await application.Renewal(context.Target ,
            context.SimultaneousUserCount.Value, context.Days.Value, context.Gigabytes.Value);

        return SafeApiResult(result);
    }
}
