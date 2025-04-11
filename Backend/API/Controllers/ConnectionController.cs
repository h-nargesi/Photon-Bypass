using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Connection;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectionController(IApplication application) : ResultHandlerController
{
    private readonly IApplication application = application;

    [HttpGet("current-con-state")]
    public ApiResult GetCurrentConnectionState([FromQuery] CurrentConnectionStateContext context)
    {
        var result = application.GetCurrentConnectionState(context);

        return SafeApiResult(result);
    }

    [HttpPost("close-con")]
    public ApiResult CloseConnection([FromBody] CloseConnectionContext context)
    {
        if (!context.Index.HasValue)
        {
            return BadRequestApiResult(message: "ایندکس خالی است!");
        }

        var result = application.CloseConnection(context);

        return SafeApiResult(result);
    }
}
