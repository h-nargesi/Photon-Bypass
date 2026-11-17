using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Portal.Basical;

public class ResultHandlerController(IJobContext context, Lazy<IAccessService> access) : ControllerBase
{
    protected IJobContext JobContext => context;

    protected string Username => User.Identity?.Name ?? string.Empty;

    protected async Task LoadJobContextAsync(string? target = null)
    {
        Log.Debug("Request.GetDisplayUrl(): {0}", Request.GetDisplayUrl());

        if (JobContext is not JobContext job)
        {
            throw new Exception("JobContext not found");
        }

        job.Username = Username;
        job.Target = target ?? Username;

        if (target == null || target == Username) return;

        if (!await access.Value.CheckAccess(Username, target))
        {
            throw new UserException("شما به این کاربر دسترسی ندارید!",
                $"Target access denied: ({target}, '{Request.GetDisplayUrl()}')",
                403);
        }
    }

    /// <summary>
    /// VPN-account variant of the target contract (Docs/22 §3): the request
    /// names the account it operates by id; access goes through the cached
    /// account set — never the raw parameter. <paramref name="target"/> stays
    /// a username for the user-level context (manager on-behalf flows).
    /// </summary>
    protected async Task LoadJobContextAsync(int? accountId, string? target = null)
    {
        await LoadJobContextAsync(target);

        if (accountId == null) return;

        if (!await access.Value.CheckAccountAccess(Username, accountId.Value))
        {
            throw new UserException("شما به این اکانت VPN دسترسی ندارید!",
                $"VPN-account access denied: ({accountId}, '{Request.GetDisplayUrl()}')",
                403);
        }

        if (JobContext is JobContext job)
        {
            job.AccountId = accountId;
        }
    }

    [NonAction]
    public static ApiResult UnauthorizedApiResult([ActionResultObjectValue] string? message = null)
    {
        return new ApiResult
        {
            Code = 401,
            Message = message
        };
    }

    [NonAction]
    public static ApiResult BadRequestApiResult([ActionResultObjectValue] short code = 400, [ActionResultObjectValue] string? message = null)
    {
        return new ApiResult
        {
            Code = code,
            Message = message
        };
    }

    [NonAction]
    public static ApiResult SafeApiResult([ActionResultObjectValue] ApiResult? result)
    {
        if (result == null)
        {
            return new ApiResult
            {
                Code = 204
            };
        }

        return result;
    }

    [NonAction]
    public static ApiResult<T> SafeApiResult<T>([ActionResultObjectValue] ApiResult<T>? result)
    {
        if (result == null)
        {
            return new ApiResult<T>
            {
                Code = 204
            };
        }

        return result;
    }
}
