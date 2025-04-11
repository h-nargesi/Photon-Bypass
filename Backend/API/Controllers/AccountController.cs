using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Domain.Model.User;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AccountController(IAccountApplication application) : ResultHandlerController
{
    private readonly IAccountApplication application = application;

    [HttpGet("full-info")]
    public async Task<ApiResult> GetFullInfo([FromQuery] FullInfoContext context)
    {
        var result = await application.GetFullInfo(context);

        return SafeApiResult(result);
    }

    [HttpPost("edit-user")]
    public async Task<ApiResult> EditUser([FromBody] EditUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email) && string.IsNullOrWhiteSpace(model.Mobile))
        {
            return BadRequestApiResult(message: "حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        var result = await application.EditUser(model);

        return SafeApiResult(result);
    }

    [HttpGet("history")]
    public async Task<ApiResult> GetHistory([FromQuery] HistoryContext context)
    {
        var result = await application.GetHistory(context);

        return SafeApiResult(result);
    }
}
