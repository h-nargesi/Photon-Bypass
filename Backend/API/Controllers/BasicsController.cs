using Microsoft.AspNetCore.Mvc;
using PhotonBypass.API.Basical;
using PhotonBypass.Application.Basics;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasicsController(
    IBasicsApplication application, Lazy<IJobContext> job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    [HttpGet("prices")]
    public async Task<ApiResult> GetPrices()
    {
        return SafeApiResult(await application.GetPrices());
    }
}
