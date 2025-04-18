using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.API.Basical;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Domain;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VpnController(
    IVpnApplication application, Lazy<IJobContext> job, Lazy<IMemoryCache> cache) :
    ResultHandlerController(job, cache)
{
    [HttpPost("change-ovpn")]
    public async Task<ApiResult> ChangeOvpnPassword([FromBody] ChangeOvpnContext context)
    {
        LoadJobContext(context.Target);

        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return BadRequestApiResult(message: "توکن خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        context.Target = JobContext.Target;

        var result = await application.ChangeOvpnPassword(context);

        return SafeApiResult(result);
    }

    [HttpGet("send-cert-email")]
    public async Task<ApiResult> SendCertEmail([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.SendCertEmail(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpGet("traffic-data")]
    public async Task<ApiResult> TrafficData([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.TrafficData(JobContext.Target);

        return SafeApiResult(result);
    }
}
