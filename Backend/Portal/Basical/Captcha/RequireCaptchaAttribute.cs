using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Basical.Captcha;

/// <summary>
/// Adaptive gate for anonymous auth endpoints. Below the failure threshold
/// the request passes untouched; once an IP is gated, the captcha answer must
/// arrive in the X-Captcha-Id / X-Captcha-Code headers (body models stay
/// clean). A missing/wrong answer short-circuits with ApiResult code 428
/// over HTTP 200 — the ApiResult convention the frontend already speaks.
/// After the action runs, the outcome feeds the adaptive counter.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCaptchaAttribute : Attribute, IAsyncActionFilter
{
    public const string IdHeader = "X-Captcha-Id";
    public const string CodeHeader = "X-Captcha-Code";

    internal const string CaptchaRequiredMessage = "کد امنیتی نامعتبر یا لازم است!";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var captcha = http.RequestServices.GetRequiredService<ICaptchaService>();
        var options = http.RequestServices.GetRequiredService<IOptions<CaptchaOptions>>().Value;
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (options.Enabled && captcha.IsRequired(ip))
        {
            var id = http.Request.Headers[IdHeader].FirstOrDefault();
            var code = http.Request.Headers[CodeHeader].FirstOrDefault();

            if (!captcha.Validate(id, code))
            {
                context.Result = new ObjectResult(new ApiResult
                {
                    Code = 428,
                    Message = CaptchaRequiredMessage,
                });
                return;
            }
        }

        var executed = await next();

        if (!options.Enabled) return;

        // one message for both shapes: exceptions surfaced by the middleware
        // (UserException → 4xx, anything else → 500) and in-band ApiResult codes
        var api_code = executed.Exception != null ? (short)500
            : executed.Result is ObjectResult { Value: ApiResult result } ? result.Code
            : (short)200;

        if (api_code >= 400) captcha.NoteFailure(ip);
        else captcha.NoteSuccess(ip);
    }
}
