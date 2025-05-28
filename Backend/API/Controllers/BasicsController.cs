using Microsoft.AspNetCore.Mvc;
using PhotonBypass.API.Basical;
using PhotonBypass.Application.Basics;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController(IBasicsApplication application) : ControllerBase
{
    [HttpGet("prices")]
    public async Task<ApiResult> GetPrices()
    {
        return ResultHandlerController.SafeApiResult(await application.GetPrices());
    }
}
