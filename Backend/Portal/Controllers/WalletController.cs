using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Application.Billing;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

public class WalletAdjustRequest
{
    public string? Target { get; set; }

    public BalanceDirection Direction { get; set; }

    public int Amount { get; set; }

    public string? Description { get; set; }
}

[ApiController]
[Route("/api/[controller]")]
public class WalletController(
    IWalletApplication application, IJobContext job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private readonly IWalletApplication application = application;

    /// <summary>Manual transparent wallet transaction — admin only (docs/20 D5).</summary>
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    [HttpPost("adjust")]
    public async Task<ApiResult> Adjust([FromBody] WalletAdjustRequest request)
    {
        await LoadJobContextAsync(request.Target);

        var result = await application.Adjust(request.Direction, request.Amount, request.Description);

        return SafeApiResult(result);
    }

    /// <summary>
    /// Wallet history of the target: the owner itself (independent accounts) or
    /// the admin — managers have no access to subordinate wallets (docs/20 §3).
    /// </summary>
    [Authorize]
    [HttpGet("history")]
    public async Task<ApiResult> GetHistory([FromQuery] string? target, [FromQuery] int page = 1)
    {
        await LoadJobContextAsync(target);

        var result = await application.GetHistory(page);

        return SafeApiResult(result);
    }
}
