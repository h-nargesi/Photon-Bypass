using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace PhotonBypass.Infra.Controller;

public class ResultHandlerController(IMemoryCache cache) : ControllerBase
{
    public string UserName => User?.Identity?.Name ?? string.Empty;

    protected string GetSafeTargetArea(string? target)
    {
        if (target == null) return UserName;

        var has_access = cache.Get<HashSet<string>>($"TargetArea|{UserName}")
            ?.Contains(target)
            ?? false;

        if (!has_access) throw new UserException("شما به این کاربر دسترسی ندارید!");

        return target;
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
