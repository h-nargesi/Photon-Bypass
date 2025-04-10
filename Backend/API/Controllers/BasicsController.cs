using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController(IApplication application) : ResultHandlerController
{
    private readonly IApplication application = application;

    [HttpGet]
    public ApiResult<IEnumerable<PriceModel>> Prices()
    {
        return SafeApiResult(application.GetPrices());
    }
}
