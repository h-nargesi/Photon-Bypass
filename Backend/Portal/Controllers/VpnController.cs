using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Portal.Context;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.ErrorHandler;
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
    /// <summary>The account switcher list (docs/22 §3.5): my accounts, or a subordinate's.</summary>
    [HttpGet("accounts")]
    public async Task<ApiResult> GetAccounts([FromQuery] string? target)
    {
        await LoadJobContextAsync(target);

        var result = await application.GetAccounts(target ?? JobContext.Target);

        return SafeApiResult(result);
    }

    // re-validates the portal password → must sit behind the auth throttle,
    // otherwise it is a brute-force bypass of the /api/auth rate limit
    [HttpPost("change-ovpn")]
    [EnableRateLimiting(ServiceFactory.AuthRateLimitPolicy)]
    public async Task<ApiResult> ChangeOvpnPassword([FromBody] ChangeOvpnContext context)
    {
        await LoadJobContextAsync(context.AccountId, context.Target);

        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return BadRequestApiResult(message: "توکن خالی است!");
        }

        // The password is optional: x-ui data-planes rotate a server-generated
        // UUID and ignore it; Radius data-planes validate it in the application.
        var user = await auth.Value.CheckUserPassword(JobContext.Username, context.Token);

        if (user.Code == 401)
        {
            Log.Debug("Wrong login password while changing VPN password (user={0}).", JobContext.Username);
            user.Code = 400;
            user.Message = "عملیات ناموفق بود!";
            return user;
        }

        var result = await application.ChangeVpnPassword(context.AccountId!.Value, context.Password ?? string.Empty);

        return SafeApiResult(result);
    }

    [HttpGet("send-cert-email")]
    public async Task<ApiResult> SendCertEmail([FromQuery] int? accountId)
    {
        await LoadJobContextAsync(accountId);

        var result = await application.SendCertEmail(accountId!.Value);

        return SafeApiResult(result);
    }

    [HttpGet("config-info")]
    public async Task<ApiResult> ConfigInfo([FromQuery] int? accountId)
    {
        await LoadJobContextAsync(accountId);

        var result = await application.GetConfigInfo(accountId!.Value);

        return SafeApiResult(result);
    }

    [HttpGet("download")]
    public async Task<IActionResult> DownloadConfig([FromQuery] int? accountId)
    {
        if (accountId == null)
        {
            throw new UserException("شناسه‌ی اکانت VPN ارسال نشده است!", "accountId query parameter is required", 400);
        }

        await LoadJobContextAsync(accountId);

        var file = await application.DownloadVpnConfig(accountId!.Value);

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("traffic-data")]
    public async Task<ApiResult> TrafficData([FromQuery] int? accountId)
    {
        await LoadJobContextAsync(accountId);

        var result = await application.TrafficData(accountId!.Value);

        return SafeApiResult(result);
    }
}
