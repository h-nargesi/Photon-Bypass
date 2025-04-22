using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.API.Basical;
using PhotonBypass.API.Context;
using PhotonBypass.Application.Plan;
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
    public ApiResult Estimate([FromBody] EstimateContext context)
    {
        LoadJobContext();

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

        var result = application.Estimate(context.Type.Value, context.SimultaneousUserCount.Value, context.Value.Value);

        return SafeApiResult(result);
    }

    [HttpPost("rnewal")]
    public async Task<ApiResult> Rnewal([FromBody] RenewalContext context)
    {
        LoadJobContext(context.Target);
        context.Target = JobContext.Target;

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

        var result = await application.Renewal(context.Target , 
            context.Type.Value, context.SimultaneousUserCount.Value, context.Value.Value);

        return SafeApiResult(result);
    }
}
