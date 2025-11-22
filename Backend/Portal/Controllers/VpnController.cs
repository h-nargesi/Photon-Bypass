using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.API.Basical;
using PhotonBypass.API.Context;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VpnController(
    IVpnApplication application, IJobContext job, Lazy<IAccessService> access, Lazy<IAuthApplication> auth) :
    ResultHandlerController(job, access)
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

        var user = await auth.Value.CheckUserPassword(JobContext.Username, context.Token);

        if (user.Code == 401)
        {
            user.Code = 400;
            user.Message = "کلمه عبور اکانت جاری اشتباه وارده شده!";
            return user;
        }

        var result = await application.ChangeOvpnPassword(JobContext.Target, context.Password);

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
