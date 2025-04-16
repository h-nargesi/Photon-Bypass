using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.API.Context;
using PhotonBypass.Application.Connection;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConnectionController(IConnectionApplication application, IMemoryCache cache) : ResultHandlerController(cache)
{
    private readonly IConnectionApplication application = application;

    [HttpGet("current-con-state")]
    public async Task<ApiResult> GetCurrentConnectionState([FromQuery] string? target)
    {
        target = GetSafeTargetArea(target);

        var result = await application.GetCurrentConnectionState(target);

        return SafeApiResult(result);
    }

    [HttpPost("close-con")]
    public async Task<ApiResult> CloseConnection([FromBody] CloseConnectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Server))
        {
            return BadRequestApiResult(message: "سرور خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.SessionId))
        {
            return BadRequestApiResult(message: "ایندکس خالی است!");
        }

        context.Target = GetSafeTargetArea(context.Target);

        var result = await application.CloseConnection(context.Server, context.Target, context.SessionId);

        return SafeApiResult(result);
    }
}
