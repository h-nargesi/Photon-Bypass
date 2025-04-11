using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PhotonBypass.Infra.Controller;

public class ResultHandlerController : ControllerBase
{
    public string UserName => User?.Identity?.Name ?? string.Empty;

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
