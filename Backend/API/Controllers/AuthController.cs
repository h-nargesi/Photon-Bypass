using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Model.Auth;
using PhotonBypass.Domain.Model.User;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthApplication application) : ResultHandlerController
{
    private readonly IAuthApplication application = application;

    [HttpPost("token")]
    public async Task<ApiResult> Login([FromBody] Domain.Model.Auth.TokenContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Username))
        {
            return BadRequestApiResult(message: "نام کاربری خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.CheckUserPassword(context);

        if (result?.Code < 300 && result.Data != null)
        {
            var token = GenerateToken(result.Data);

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

        return SafeApiResult(result);
    }

    [HttpPost("reset-pass")]
    public async Task<ApiResult> ResetPassword([FromBody] ResetPasswordContext context)
    {
        if (string.IsNullOrWhiteSpace(context.EmailMobile))
        {
            return BadRequestApiResult(message: "ایمیل/موبایل خالی است!");
        }

        var result = await application.ResetPassword(context);

        return SafeApiResult(result);
    }

    [HttpPost("register")]
    public async Task<ApiResult> Register([FromBody] RegisterContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Username))
        {
            return BadRequestApiResult(message: "نام کاربری خالی است!");
        }

        if (string.IsNullOrWhiteSpace(context.Email) && string.IsNullOrWhiteSpace(context.Mobile))
        {
            return BadRequestApiResult(message: "حداقل یکی از دو فیلد موبایل یا ایمیل باید پر باشد!");
        }

        if (string.IsNullOrWhiteSpace(context.Password))
        {
            return BadRequestApiResult(message: "کلمه عبور خالی است!");
        }

        var result = await application.Register(context);

        return SafeApiResult(result);
    }

    private static string GenerateToken(FullUserModel user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IdentityHelper.TokenKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Name, (user.Firstname + " " + user.Lastname).Trim())
        };

        var token = new JwtSecurityToken(
            expires: DateTime.Now.AddHours(1),
            claims: claims,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
