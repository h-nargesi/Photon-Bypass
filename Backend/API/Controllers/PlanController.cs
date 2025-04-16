using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PlanController(IPlanApplication application) : ResultHandlerController
{
    private readonly IPlanApplication application = application;

    [HttpGet("plan-state")]
    public async Task<ApiResult> GetPlanState([FromQuery] string? target)
    {
        target = GetSafeTargetArea(target);

        var result = await application.GetPlanState(target);

        return SafeApiResult(result);
    }

    [HttpGet("plan-info")]
    public async Task<ApiResult> GetPlanInfo([FromQuery] string? target)
    {
        target = GetSafeTargetArea(target);

        var result = await application.GetPlanInfo(target);

        return SafeApiResult(result);
    }

    [HttpPost("estimate")]
    public async Task<ApiResult> Estimate([FromBody] RenewalContext context)
    {
        var result = RenewalContextCheck(context);

        context.Target = GetSafeTargetArea(context.Target);

        result ??= await application.Estimate(context);

        return SafeApiResult(result);
    }

    [HttpPost("renewal")]
    public async Task<ApiResult> Renewal([FromBody] RenewalContext context)
    {
        var result = RenewalContextCheck(context);

        context.Target = GetSafeTargetArea(context.Target);

        result ??= await application.Rnewal(context);

        return SafeApiResult(result);
    }

    private static ApiResult? RenewalContextCheck(RenewalContext context)
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
