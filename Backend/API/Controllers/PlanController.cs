using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.API.Basical;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PlanController(
    IPlanApplication application, Lazy<IJobContext> job, Lazy<IMemoryCache> cache) :
    ResultHandlerController(job, cache)
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
        context.Target = JobContext.Target;

        var result = RnewalContextCheck(context);

        result ??= await application.Estimate(context);

        return SafeApiResult(result);
    }

    [HttpPost("rnewal")]
    public async Task<ApiResult> Rnewal([FromBody] RenewalContext context)
    {
        LoadJobContext(context.Target);
        context.Target = JobContext.Target;

        var result = RnewalContextCheck(context);

        result ??= await application.Renewal(context);

        return SafeApiResult(result);
    }

    private static ApiResult? RnewalContextCheck(RenewalContext context)
    {
        if (!context.Type.HasValue)
        {
            return BadRequestApiResult(message: "نوع پلن مشخص نشده است!");
        }

        if (!context.Value.HasValue)
        {
            return BadRequestApiResult(message: "مقدار درخواست پلن مشخص نشده است!");
        }

        if (!context.SimultaneousUserCount.HasValue)
        {
            return BadRequestApiResult(message: "تعداد کاربران مشخص نشده است!");
        }

        return null;
    }
}
