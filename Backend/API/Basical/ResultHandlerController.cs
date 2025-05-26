using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.API.Basical;

public class ResultHandlerController(Lazy<IJobContext> context, Lazy<IAccessService> access) : ControllerBase
{
    protected Lazy<IJobContext> JobContext { get; } = context;

    protected string Username => User?.Identity?.Name ?? string.Empty;

    protected void LoadJobContext(string? target = null)
    {
        Log.Debug("Request.GetDisplayUrl(): {0}", Request.GetDisplayUrl());

        if (JobContext.Value is not JobContext job)
        {
            throw new Exception("JobContext not found");
        }

        job.Username = Username;
        job.Target = target ?? Username;

        if (target == null) return;

        if (!access.Value.CheckAccess(Username, target))
        {
            throw new UserException("شما به این کاربر دسترسی ندارید!",
                $"Target access denied: ({target}, '{Request.GetDisplayUrl()}')");
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
