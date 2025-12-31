using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Application.Billing;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class BillingController(
    IBillingApplication application, IJobContext job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private readonly IBillingApplication application = application;

    [Authorize]
    [HttpPost("pay")]
    public async Task<ApiResult> Pay([FromBody] int value)
    {
        LoadJobContext();

        var result = await application.GenerateInvoiceCode(value);

        return SafeApiResult(result);
    }

    [Authorize]
    [HttpGet("get-invoice")]
    public async Task<ApiResult> GetInvoice([FromQuery] int code)
    {
        LoadJobContext();

        var result = await application.GetInvoice(code);

        return SafeApiResult(result);
    }

    [HttpGet("payment-callback")]
    public async Task<ApiResult> PaymentCallback([FromQuery] string code)
    {
        LoadJobContext();

        var result = await application.PaymentCallback(code);

        return SafeApiResult(result);
    }
}
