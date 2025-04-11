using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Plan;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlanController(IApplication application) : ResultHandlerController
{
    private readonly IApplication application = application;

    [HttpGet("plan-state")]
    public ApiResult GetPlanState([FromQuery] PlanStateContext context)
    {
        var result = application.GetPlanState(context);

        return SafeApiResult(result);
    }

    [HttpGet("plan-info")]
    public ApiResult GetPlanInfo([FromQuery] PlanInfoContext context)
    {
        var result = application.GetPlanInfo(context);

        return SafeApiResult(result);
    }

    [HttpPost("estimate")]
    public ApiResult Estimate([FromBody] RnewalContext context)
    {
        var result = RnewalContextCheck(context);

        result ??= application.Estimate(context);

        return SafeApiResult(result);
    }

    [HttpPost("rnewal")]
    public ApiResult Rnewal([FromBody] RnewalContext context)
    {
        var result = RnewalContextCheck(context);

        result ??= application.Rnewal(context);

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
