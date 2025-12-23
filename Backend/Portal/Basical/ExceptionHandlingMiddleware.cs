using System.Net.Mime;
using System.Text.Json;
using PhotonBypass.Domain;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Portal.Basical;

public class ExceptionHandlingMiddleware(RequestDelegate next)
{
    public const string ErrorMessage = "خطای غیرمنتظره‌ای رخ داده است!";

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var job = context.RequestServices.GetRequiredService<IJobContext>();
            var api_result = GetApiResult(ex, out var http_code);
            var log_message = GetMessage(ex);

            if (api_result.Code == 400)
            {
                Log.Warning("[user: {0}] {1}", job.Username, log_message);
            }
            else
            {
                Log.Error("[user: {0}] {1}", job.Username, log_message);
            }

            context.Response.StatusCode = http_code;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            await context.Response.WriteAsync(JsonSerializer.Serialize(api_result));
        }
    }

    protected static string GetMessage(Exception ex)
    {
        return ex.Message + "\n" + ex.StackTrace;
    }

    protected virtual ApiResult GetApiResult(Exception ex, out short http_code)
    {
        ApiResult result;

        if (ex is UserException uex)
        {
            http_code = uex.HttpCode ?? StatusCodes.Status400BadRequest;
            result = new ApiResult
            {
                Code = http_code,
                Message = uex.UserMessage ?? ErrorMessage,
            };
        }
        else
        {
            http_code = StatusCodes.Status500InternalServerError;
            result = new ApiResult
            {
                Code = http_code,
                Message = ErrorMessage,
            };
        }

        return result;
    }
}

public class ExceptionHandlingMiddlewareInDevelopment(RequestDelegate next)
    : ExceptionHandlingMiddleware(next)
{
    protected override ApiResult GetApiResult(Exception ex, out short http_code)
    {
        var result = base.GetApiResult(ex, out http_code);

        result.Developer = GetMessage(ex);

        return result;
    }
}