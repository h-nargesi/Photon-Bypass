using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController(IApplication application) : ResultHandlerController
{
    private readonly IApplication application = application;

    [HttpGet("prices")]
    public ApiResult GetPrices()
    {
        return SafeApiResult(application.GetPrices());
    }
}
