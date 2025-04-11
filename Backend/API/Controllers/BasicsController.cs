using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController(IBasicsApplication application) : ResultHandlerController
{
    private readonly IBasicsApplication application = application;

    [HttpGet("prices")]
    public async Task<ApiResult> GetPrices()
    {
        return SafeApiResult(await application.GetPrices());
    }
}
