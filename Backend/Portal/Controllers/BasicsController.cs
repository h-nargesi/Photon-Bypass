using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Application.Basics;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class BasicsController(IBasicsApplication application) : ControllerBase
{
    [HttpGet("prices")]
    public async Task<ApiResult> GetPrices()
    {
        return ResultHandlerController.SafeApiResult(await application.GetPrices());
    }
}
