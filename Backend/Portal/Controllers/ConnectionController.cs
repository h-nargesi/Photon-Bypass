using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Portal.Context;
using PhotonBypass.Application.Connection;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

[Authorize]
[ApiController]
[Route("/api/[controller]")]
public class ConnectionController(
    IConnectionApplication application, IJobContext job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    [HttpGet("current-con-state")]
    public async Task<ApiResult> GetCurrentConnectionState([FromQuery] int? accountId)
    {
        if (accountId == null)
        {
            return BadRequestApiResult(message: "شناسه‌ی اکانت VPN ارسال نشده است!");
        }

        await LoadJobContextAsync(accountId);

        var result = await application.GetCurrentConnectionState(accountId.Value);

        return SafeApiResult(result);
    }

    [HttpPost("close-con")]
    public async Task<ApiResult> CloseConnection([FromBody] CloseConnectionContext context)
    {
        if (context.AccountId == null)
        {
            return BadRequestApiResult(message: "شناسه‌ی اکانت VPN ارسال نشده است!");
        }

        await LoadJobContextAsync(context.AccountId);

        if (string.IsNullOrWhiteSpace(context.Server))
        {
            return BadRequestApiResult(message: "سرور خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.SessionId))
        {
            return BadRequestApiResult(message: "کد اتصال خالی است!");
        }

        var result = await application.CloseConnection(context.Server, context.AccountId!.Value, context.SessionId);

        return SafeApiResult(result);
    }
}
