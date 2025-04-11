using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Connection;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConnectionController(IConnectionApplication application) : ResultHandlerController
{
    private readonly IConnectionApplication application = application;

    [HttpGet("current-con-state")]
    public async Task<ApiResult> GetCurrentConnectionState([FromQuery] string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            target = UserName;
        }

        var result = await application.GetCurrentConnectionState(target);

        return SafeApiResult(result);
    }

    [HttpPost("close-con")]
    public async Task<ApiResult> CloseConnection([FromBody] CloseConnectionContext context)
    {
        if (!context.Index.HasValue)
        {
            return BadRequestApiResult(message: "ایندکس خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.Target = UserName;
        }

        var result = await application.CloseConnection(context);

        return SafeApiResult(result);
    }
}
