using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VpnController(IVpnApplication application, IMemoryCache cache) : ResultHandlerController(cache)
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

        context.Target = GetSafeTargetArea(context.Target);

        var result = await application.ChangeOvpnPassword(context);

        return SafeApiResult(result);
    }

    [HttpGet("send-cert-email")]
    public async Task<ApiResult> SendCertEmail([FromQuery] string? target)
    {
        target = GetSafeTargetArea(target);

        var result = await application.SendCertEmail(target);

        return SafeApiResult(result);
    }

    [HttpGet("traffic-data")]
    public async Task<ApiResult> TrafficData([FromQuery] string? target)
    {
        target = GetSafeTargetArea(target);

        var result = await application.TrafficData(target);

        return SafeApiResult(result);
    }
}
