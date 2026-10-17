using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

public class ServerSetActiveRequest
{
    public int Id { get; set; }

    public bool Active { get; set; }
}

/// <summary>
/// Admin server CRUD (docs/20 phase 5, D8). The list never carries the stored
/// config — only a has-config flag; create/update accept a plain config JSON
/// which is protected server-side (write-without-read).
/// </summary>
[ApiController]
[Route("/api/[controller]")]
public class ServerController(
    IServerCrudApplication application, IJobContext job, Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private readonly IServerCrudApplication application = application;

    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    [HttpGet("list")]
    public async Task<ApiResult> GetList()
    {
        LoadJobContext();

        var result = await application.GetList();

        return SafeApiResult(result);
    }

    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    [HttpPost("create")]
    public async Task<ApiResult> Create([FromBody] SaveServerModel request)
    {
        LoadJobContext();

        var result = await application.Create(request);

        return SafeApiResult(result);
    }

    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    [HttpPost("update")]
    public async Task<ApiResult> Update([FromBody] SaveServerModel request)
    {
        LoadJobContext();

        var result = await application.Update(request);

        return SafeApiResult(result);
    }

    /// <summary>
    /// Activate/deactivate — two-way lifecycle toggle (D8 "full CRUD"; the
    /// one-way `deactivate` of the original step list had no reactivation
    /// path, same recorded deviation as the phase-4 user toggle).
    /// </summary>
    [Authorize(Policy = ServiceFactory.AdminPolicy)]
    [HttpPost("set-active")]
    public async Task<ApiResult> SetActive([FromBody] ServerSetActiveRequest request)
    {
        LoadJobContext();

        var result = await application.SetActive(request.Id, request.Active);

        return SafeApiResult(result);
    }
}
