using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PhotonBypass.Application.Account;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.OutSource;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Portal.Context;
using PhotonBypass.Result;

namespace PhotonBypass.Portal.Controllers;

[Authorize]
[ApiController]
[Route("/api/[controller]")]
public class AccountController(
    IAccountApplication application, IJobContext job, Lazy<IAccessService> access,
    IOptions<TelegramOptions> telegram_options) :
    ResultHandlerController(job, access)
{
    [HttpGet("get-user")]
    public async Task<ApiResult> GetUser()
    {
        await LoadJobContextAsync();

        var result = await application.GetUser(Username);

        return SafeApiResult(result);
    }

    [HttpGet("full-info")]
    public async Task<ApiResult> GetFullInfo([FromQuery] string? target)
    {
        await LoadJobContextAsync(target);

        var result = await application.GetFullInfo(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpPost("edit-user")]
    public async Task<ApiResult> EditUser([FromQuery] string? target, [FromBody] EditUserModel context)
    {
        await LoadJobContextAsync(target);

        var result = await application.EditUser(JobContext.Target, context);

        return SafeApiResult(result);
    }

    // verifies the current password → shares the auth rate-limit bucket so it
    // cannot be used as an unthrottled oracle for the portal password
    [HttpPost("change-pass")]
    [EnableRateLimiting(ServiceFactory.AuthRateLimitPolicy)]
    public async Task<ApiResult> ChangePassword([FromBody] ChangePasswordContext context)
    {
        await LoadJobContextAsync();

        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return BadRequestApiResult(message: "کلمه عبور قبلی خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.ChangePassword(Username, context.Token, context.Password);

        return SafeApiResult(result);
    }

    [HttpGet("history")]
    public async Task<ApiResult> GetHistory([FromQuery] string? target, [FromQuery] HistoryContext context)
    {
        await LoadJobContextAsync(target);

        var result = await application.GetHistory(JobContext.Target, context.FromDateTime, context.ToDateTime);

        return SafeApiResult(result);
    }

    // telegram link flow (D2): the deep link is completed here because only
    // the host layer sees both the issued code and the bot username
    [HttpGet("telegram-link")]
    public async Task<ApiResult> GetTelegramLink([FromQuery] string? target)
    {
        await LoadJobContextAsync(target);

        var result = await application.GetTelegramLinkStatus(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpPost("telegram-link")]
    public async Task<ApiResult> CreateTelegramLink([FromQuery] string? target)
    {
        await LoadJobContextAsync(target);

        var result = await application.CreateTelegramLink(JobContext.Target);

        if (result.Data?.Code is { } code)
        {
            result.Data.Url = $"https://t.me/{telegram_options.Value.BotUsername}?start={code}";
        }

        return SafeApiResult(result);
    }

    [HttpDelete("telegram-link")]
    public async Task<ApiResult> DisconnectTelegram([FromQuery] string? target)
    {
        await LoadJobContextAsync(target);

        var result = await application.DisconnectTelegram(JobContext.Target);

        return SafeApiResult(result);
    }
}
