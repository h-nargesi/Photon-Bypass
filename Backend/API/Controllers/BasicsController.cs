using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.API.Basical;
using PhotonBypass.Application.Basics;
using PhotonBypass.Domain;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController(
    IBasicsApplication application, Lazy<IJobContext> job, Lazy<IMemoryCache> cache) :
    ResultHandlerController(job, cache)
{
    [HttpGet("prices")]
    public async Task<ApiResult> GetPrices()
    {
        return SafeApiResult(await application.GetPrices());
    }
}
