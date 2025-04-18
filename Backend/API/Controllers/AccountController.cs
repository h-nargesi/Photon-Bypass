using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.API.Basical;
using PhotonBypass.API.Context;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AccountController(IAccountApplication application) : ResultHandlerController
{
    private readonly IAccountApplication application = application;

    [HttpGet("get-user")]
    public async Task<ApiResult> GetUser()
    {
        var result = await application.GetUser(UserName);

        return SafeApiResult(result);
    }

    [HttpGet("full-info")]
    public async Task<ApiResult> GetFullInfo([FromQuery] string? target)
    {
        target = GetSafeTargetArea(target);

        var result = await application.GetFullInfo(target);

        return SafeApiResult(result);
    }

    [HttpPost("edit-user")]
    public async Task<ApiResult> EditUser([FromQuery] string? target, [FromBody] EditUserContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Email) && string.IsNullOrWhiteSpace(context.Mobile))
        {
            return BadRequestApiResult(message: "حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        target = GetSafeTargetArea(target);

        var result = await application.EditUser(target, context);

        return SafeApiResult(result);
    }

    [HttpPost("change-pass")]
    public async Task<ApiResult> ChangePassword([FromBody] ChangePasswordContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return BadRequestApiResult(message: "کلمه عبور قبلی خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.ChangePassword(UserName, context.Token, context.Password);

        return SafeApiResult(result);
    }

    [HttpGet("history")]
    public async Task<ApiResult> GetHistory([FromQuery] HistoryContext context)
    {
        context.Target = GetSafeTargetArea(context.Target);

        var result = await application.GetHistory(context.Target, context.From, context.To);

        return SafeApiResult(result);
    }
}
