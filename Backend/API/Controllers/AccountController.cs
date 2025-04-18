﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.API.Basical;
using PhotonBypass.API.Context;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain;
using PhotonBypass.Result;

namespace PhotonBypass.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AccountController(
    IAccountApplication application, Lazy<IJobContext> job, Lazy<IMemoryCache> cache) : 
    ResultHandlerController(job, cache)
{
    private readonly IAccountApplication application = application;

    [HttpGet("get-user")]
    public async Task<ApiResult> GetUser()
    {
        LoadJobContext();

        var result = await application.GetUser(Username);

        return SafeApiResult(result);
    }

    [HttpGet("full-info")]
    public async Task<ApiResult> GetFullInfo([FromQuery] string? target)
    {
        LoadJobContext(target);

        var result = await application.GetFullInfo(JobContext.Target);

        return SafeApiResult(result);
    }

    [HttpPost("edit-user")]
    public async Task<ApiResult> EditUser([FromQuery] string? target, [FromBody] EditUserContext context)
    {
        LoadJobContext(target);

        var result = await application.EditUser(JobContext.Target, context);

        return SafeApiResult(result);
    }

    [HttpPost("change-pass")]
    public async Task<ApiResult> ChangePassword([FromBody] ChangePasswordContext context)
    {
        LoadJobContext();

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
        LoadJobContext(target);

        var result = await application.GetHistory(JobContext.Target, context.From, context.To);

        return SafeApiResult(result);
    }
}
