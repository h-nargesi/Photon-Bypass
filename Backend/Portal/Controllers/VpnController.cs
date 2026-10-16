using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Portal.Context;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Portal.Controllers;

[Authorize]
[ApiController]
[Route("/api/[controller]")]
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
            Log.Debug("Wrong login password while changing VPN password (user={0}).", JobContext.Username);
            user.Code = 400;
            user.Message = "عملیات ناموفق بود!";
            return user;
        }

        var result = await application.ChangeVpnPassword(JobContext.Target, context.Password);

        return SafeApiResult(result);
    }

    [HttpGet("send-cert-email")]
    public async Task<ApiResult> SendCertEmail([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.SendCertEmail(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpGet("download-cert")]
    public async Task<IActionResult> DownloadCert([FromQuery] string? target)
    {
        LoadJobContext(target);

        var cert = await application.DownloadCertificate(JobContext.Target);

        return File(cert.CertFile, "application/x-openvpn-profile", $"{JobContext.Target}.ovpn");
    }

    [HttpGet("traffic-data")]
    public async Task<ApiResult> TrafficData([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.TrafficData(JobContext.Target);

        return SafeApiResult(result);
    }
}
