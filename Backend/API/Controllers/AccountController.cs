using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController(IApplication application) : ResultHandlerController
{
    private readonly IApplication application = application;

    [HttpPost("change-ovpn")]
    public ApiResult ChangeOvpnPassword([FromBody] ChangeOvpnContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return BadRequestApiResult(message: "توکن خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = application.ChangeOvpnPassword(context);
        
        return SafeApiResult(result);
    }

    [HttpGet("send-cert-email")]
    public ApiResult SendCertEmail([FromQuery] SendCertEmailContext context)
    {
        var result = application.SendCertEmail(context);

        return SafeApiResult(result);
    }

    [HttpGet("traffic-data")]
    public ApiResult<TrafficDataModel> TrafficData([FromQuery] TrafficDataContext context)
    {
        var result = application.TrafficData(context);

        return SafeApiResult(result);
    }

    [HttpGet("full-info")]
    public ApiResult<FullUserModel> GetFullInfo([FromQuery] FullInfoContext context)
    {
        var result = application.GetFullInfo(context);

        return SafeApiResult(result);
    }

    [HttpPost("edit-user")]
    public ApiResult EditUser([FromBody] EditUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Mobile))
        {
            return BadRequestApiResult(message: "حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        var result = application.EditUser(model);

        return SafeApiResult(result);
    }

    [HttpGet("history")]
    public ApiResult<HistoryModel[]> GetHistory([FromQuery] HistoryContext context)
    {
        var result = application.GetHistory(context);

        return SafeApiResult(result);
    }
}
