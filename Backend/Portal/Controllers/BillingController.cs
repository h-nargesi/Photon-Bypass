using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

public class PayRequest
{
    public int Value { get; set; }

    public string? Target { get; set; }
}

public class SettleWalletRequest
{
    public int Code { get; set; }

    public string? Target { get; set; }
}

[ApiController]
[Route("/api/[controller]")]
public class BillingController(
    IBillingApplication application, IJobContext job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private readonly IBillingApplication application = application;

    [Authorize]
    [HttpPost("pay")]
    public async Task<ApiResult> Pay([FromBody] PayRequest request)
    {
        LoadJobContext(request.Target);

        var result = await application.IssueTopUp(request.Value);

        return SafeApiResult(result);
    }

    [Authorize]
    [HttpGet("get-invoice")]
    public async Task<ApiResult> GetInvoice([FromQuery] int code, [FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.GetInvoice(code);

        return SafeApiResult(result);
    }

    [Authorize]
    [HttpPost("register-receipt")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3_145_728)]
    public async Task<ApiResult> RegisterReceipt([FromForm] int code, [FromForm] string? target, IFormFile? file, [FromForm] string? text)
    {
        LoadJobContext(target);

        byte[]? image = null;
        string? file_name = null;
        string? content_type = null;

        if (file is { Length: > 0 })
        {
            if (file.Length > 2_097_152)
            {
                throw new UserException("حجم تصویر رسید بیش از حد مجاز است!",
                    $"Receipt file too large: {file.Length}");
            }

            using var stream = file.OpenReadStream();

            using var reader = new MemoryStream();
            await stream.CopyToAsync(reader);

            image = reader.ToArray();
            file_name = file.FileName;
            content_type = file.ContentType;
        }

        var result = await application.RegisterReceipt(code, image, file_name, content_type, text);

        return SafeApiResult(result);
    }

    [Authorize]
    [HttpPost("settle-wallet")]
    public async Task<ApiResult> SettleWallet([FromBody] SettleWalletRequest request)
    {
        LoadJobContext(request.Target);

        var result = await application.SettleWallet(request.Code);

        return SafeApiResult(result);
    }
}
