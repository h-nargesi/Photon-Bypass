using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VpnController(IVpnApplication application) : ResultHandlerController
{
    private readonly IVpnApplication application = application;

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
}
