using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Plan;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PlanController(IPlanApplication application) : ResultHandlerController
{
    private readonly IPlanApplication application = application;

    [HttpGet("plan-state")]
    public async Task<ApiResult> GetPlanState([FromQuery] PlanStateContext context)
    {
        var result = await application.GetPlanState(context);

        return SafeApiResult(result);
    }

    [HttpGet("plan-info")]
    public async Task<ApiResult> GetPlanInfo([FromQuery] PlanInfoContext context)
    {
        var result = await application.GetPlanInfo(context);

        return SafeApiResult(result);
    }

    [HttpPost("estimate")]
    public async Task<ApiResult> Estimate([FromBody] RnewalContext context)
    {
        var result = RnewalContextCheck(context);

        result ??= await application.Estimate(context);

        return SafeApiResult(result);
    }

    [HttpPost("rnewal")]
    public async Task<ApiResult> Rnewal([FromBody] RnewalContext context)
    {
        var result = RnewalContextCheck(context);

        result ??= await application.Rnewal(context);

        return SafeApiResult(result);
    }

    private static ApiResult? RnewalContextCheck(RnewalContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            return BadRequestApiResult(message: "کاربر مورد نظر مشخص نشده است!");
        }

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
