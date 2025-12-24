using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Result;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PhotonBypass.Portal.Context;

namespace PhotonBypass.Portal.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class AuthController(
    IAuthApplication application,
    IJobContext job,
    Lazy<IAccessService> access) :
    ResultHandlerController(job, access)
{
    private Lazy<IAccessService> AccessSrv { get; } = access;

    [HttpPost("token")]
    public async Task<ApiResult> Login([FromBody] Context.TokenContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Username))
        {
            return BadRequestApiResult(message: "نام کاربری خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.CheckUserPassword(context.Username, context.Password);

        if (result is not { Code: < 300, Data: not null }) return SafeApiResult(result);

        var token = GenerateToken(result.Data);

        AccessSrv.Value.LoginEvent(result.Data.Username, [.. result.Data.TargetArea.Keys]);

        return new ApiResult<object>
        {
            Code = result.Code,
            Message = result.Message,
            Developer = result.Developer,
            MessageMethod = result.MessageMethod,
            Data = new
            {
                access_token = token,
                token_type = "Bearer",
                expires_in = 3600
            }
        };
    }

    [HttpPost("forget-pass")]
    public async Task<ApiResult> ForgetPassword([FromBody] ResetPasswordContext context)
    {
        if (string.IsNullOrWhiteSpace(context.EmailMobile))
        {
            return BadRequestApiResult(message: "ایمیل/موبایل خالی است!");
        }

        var result = await application.ForgetPassword(context.EmailMobile);

        return SafeApiResult(result);
    }

    [HttpPost("reset-pass")]
    public async Task<ApiResult> ResetPassword([FromBody] ChangePasswordContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return BadRequestApiResult(message: "کد خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.ResetPassword(context.Token, context.Password);

        return SafeApiResult(result);
    }

    [HttpPost("register")]
    public async Task<ApiResult> Register([FromBody] RegisterModel context)
    {
        if (string.IsNullOrWhiteSpace(context.Username))
        {
            return BadRequestApiResult(message: "نام کاربری خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.Register(context);

        return SafeApiResult(result);
    }

    private static string GenerateToken(TargetModel user)
    {
        var creds = new SigningCredentials(IdentityHelper.Key, SecurityAlgorithms.HmacSha512Signature);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Name, user.Fullname ?? string.Empty),
        };

        var token = new JwtSecurityToken(
            issuer: IdentityHelper.Issuer,
            audience: IdentityHelper.Issuer,
            expires: DateTime.Now.AddHours(1),
            claims: claims,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}