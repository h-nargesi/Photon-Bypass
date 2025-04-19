using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.API.Basical;
using PhotonBypass.API.Context;
using PhotonBypass.Application.Connection;
using PhotonBypass.Domain;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConnectionController(
    IConnectionApplication application, Lazy<IJobContext> job, Lazy<IMemoryCache> cache) :
    ResultHandlerController(job, cache)
{
    [HttpGet("current-con-state")]
    public async Task<ApiResult> GetCurrentConnectionState([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.GetCurrentConnectionState(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpPost("close-con")]
    public async Task<ApiResult> CloseConnection([FromBody] string? target, [FromBody] CloseConnectionContext context)
    {
        LoadJobContext(target);

        if (string.IsNullOrWhiteSpace(context.Server))
        {
            return BadRequestApiResult(message: "سرور خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.SessionId))
        {
            return BadRequestApiResult(message: "کد اتصال خالی است!");
        }

        var result = await application.CloseConnection(context.Server, JobContext.Target, context.SessionId);

        return SafeApiResult(result);
    }
}
