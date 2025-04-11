using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Domain.Model.User;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AccountController(IApplication application) : ResultHandlerController
{
    private readonly IApplication application = application;

    [HttpPost("change-ovpn")]
    public async Task<ApiResult> ChangeOvpnPassword([FromBody] ChangeOvpnContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return BadRequestApiResult(message: "توکن خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.ChangeOvpnPassword(context);

        return SafeApiResult(result);
    }

    [HttpGet("send-cert-email")]
    public async Task<ApiResult> SendCertEmail([FromQuery] SendCertEmailContext context)
    {
        var result = await application.SendCertEmail(context);

        return SafeApiResult(result);
    }

    [HttpGet("traffic-data")]
    public async Task<ApiResult> TrafficData([FromQuery] TrafficDataContext context)
    {
        var result = await application.TrafficData(context);

        return SafeApiResult(result);
    }

    [HttpGet("full-info")]
    public async Task<ApiResult> GetFullInfo([FromQuery] FullInfoContext context)
    {
        var result = await application.GetFullInfo(context);

        return SafeApiResult(result);
    }

    [HttpPost("edit-user")]
    public async Task<ApiResult> EditUser([FromBody] EditUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Mobile))
        {
            return BadRequestApiResult(message: "حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        var result = await application.EditUser(model);

        return SafeApiResult(result);
    }

    [HttpGet("history")]
    public async Task<ApiResult> GetHistory([FromQuery] HistoryContext context)
    {
        var result = await application.GetHistory(context);

        return SafeApiResult(result);
    }
}
