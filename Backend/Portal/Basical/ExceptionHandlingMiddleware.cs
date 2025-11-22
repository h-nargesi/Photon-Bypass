using System.Net.Mime;
using System.Text.Json;
using PhotonBypass.Domain;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.API.Basical;

public class ExceptionHandlingMiddleware(RequestDelegate next, IJobContext job)
{
    public const string ERROR_MESSAGE = "خطای غیرمنتظره‌ای رخ داده است!";

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var api_result = GetApiResult(ex);

            if (api_result.Code == 400)
            {
                Log.Warning($"[user: {job.Username}]" + GetMessage(ex));
            }
            else
            {
                Log.Error($"[user: {job.Username}]" + GetMessage(ex));
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            await context.Response.WriteAsync(JsonSerializer.Serialize(api_result));
        } 
    }

    protected static string GetMessage(Exception ex)
    {
        return ex.Message + "\n" + ex.StackTrace;
    }

    protected virtual ApiResult GetApiResult(Exception ex)
    {
        ApiResult result;

        if (ex is UserException uex)
        {
            result = new ApiResult
            {
                Code = 400,
                Message = uex.UserMessage ?? ERROR_MESSAGE,
            };
        }
        else
        {
            result = new ApiResult
            {
                Code = 500,
                Message = ERROR_MESSAGE,
            };
        }

        return result;
    }
}

public class ExceptionHandlingMiddlewareInDevelopment(RequestDelegate next, IJobContext job)
    : ExceptionHandlingMiddleware(next, job)
{
    protected override ApiResult GetApiResult(Exception ex)
    {
        var result = base.GetApiResult(ex);

        result.Developer = GetMessage(ex);

        return result;
    }
}
