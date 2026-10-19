using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

public class SetPasswordRequest
{
    public string? Target { get; set; }

    public string? Password { get; set; }
}

public class SetRoleRequest
{
    public string? Target { get; set; }

    public AccountRole? Role { get; set; }
}

public class SetOwnerRequest
{
    public string? Target { get; set; }

    public string? Owner { get; set; }
}

public class SetSelfServiceRequest
{
    public string? Target { get; set; }

    public bool Allow { get; set; }
}

public class SetStatusRequest
{
    public string? Target { get; set; }

    public AccountStatus? Status { get; set; }
}

/// <summary>
/// Manager/admin user management (docs/20 phase 4). Note: these endpoints
/// never route <c>target</c> through <see cref="ResultHandlerController.LoadJobContextAsync(string?)"/>'s
/// cached access check — the application validates the actor→target
/// relationship live against the database, so freshly created or attached
/// subordinates are manageable in the same session.
/// </summary>
[ApiController]
[Route("/api/[controller]")]
public class UserController(
    IUserManagementApplication application, IJobContext job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private readonly IUserManagementApplication application = application;

    /// <summary>Paged user list — manager sees own subordinates, admin sees everyone.</summary>
    [Authorize(Policy = ServiceFactory.ManagerPolicy)]
    [HttpGet("list")]
    public async Task<ApiResult> GetList([FromQuery] string? search, [FromQuery] int page = 1)
    {
        await LoadJobContextAsync();

        var result = await application.GetList(search, page);

        return SafeApiResult(result);
    }

    [Authorize(Policy = ServiceFactory.ManagerPolicy)]
    [HttpPost("create")]
    public async Task<ApiResult> Create([FromBody] CreateUserModel request)
    {
        await LoadJobContextAsync();

        var result = await application.Create(request);

        return SafeApiResult(result);
    }

    [Authorize(Policy = ServiceFactory.ManagerPolicy)]
    [HttpPost("set-password")]
    public async Task<ApiResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        await LoadJobContextAsync();

        var result = await application.SetPassword(request.Target!, request.Password!);

        return SafeApiResult(result);
    }

    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    [HttpPost("set-role")]
    public async Task<ApiResult> SetRole([FromBody] SetRoleRequest request)
    {
        await LoadJobContextAsync();

        var result = await application.SetRole(request.Target!, request.Role ?? AccountRole.User);

        return SafeApiResult(result);
    }

    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    [HttpPost("set-owner")]
    public async Task<ApiResult> SetOwner([FromBody] SetOwnerRequest request)
    {
        await LoadJobContextAsync();

        var result = await application.SetOwner(request.Target!, request.Owner);

        return SafeApiResult(result);
    }

    [Authorize(Policy = ServiceFactory.ManagerPolicy)]
    [HttpPost("set-self-service")]
    public async Task<ApiResult> SetSelfService([FromBody] SetSelfServiceRequest request)
    {
        await LoadJobContextAsync();

        var result = await application.SetSelfService(request.Target!, request.Allow);

        return SafeApiResult(result);
    }

    /// <summary>
    /// Two-stage status change (docs/20 D13): Manager may suspend/deactivate
    /// its own subordinates; the immediate VPN sync is best-effort.
    /// </summary>
    [Authorize(Policy = ServiceFactory.ManagerPolicy)]
    [HttpPost("set-status")]
    public async Task<ApiResult> SetStatus([FromBody] SetStatusRequest request)
    {
        await LoadJobContextAsync();

        if (request.Status is not { } status)
        {
            throw new UserException("وضعیت کاربر مشخص نشده!", "set-status requires a Status value.");
        }

        var result = await application.SetStatus(request.Target!, status);

        return SafeApiResult(result);
    }
}
