using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Basical.Captcha;
using PhotonBypass.Portal.Context;
using PhotonBypass.Result;
using PhotonBypass.Test.Initializer;
using System.Net.Http.Json;
using System.Text.Json;

namespace PhotonBypass.Test.Facts.Scenario;

/// <summary>
/// End-to-end adaptive-captcha coverage on a dedicated host (the shared
/// scenario factory keeps captcha disabled). The in-memory host shares the
/// IMemoryCache singleton with the test, so the correct captcha answer can be
/// read straight from the store the service wrote it to.
/// </summary>
public class CaptchaTest(CaptchaTest.CaptchaFactory factory)
    : HttpHandler(factory), IClassFixture<CaptchaTest.CaptchaFactory>
{
    private const int Threshold = 2;

    private static readonly JsonSerializerOptions BodyOptions = new() { PropertyNameCaseInsensitive = true };

    private IMemoryCache Cache => ServiceProvider.GetRequiredService<IMemoryCache>();

    [Fact]
    public async Task CaptchaEndpoint_IssuesPngDataUrl()
    {
        var first = await FetchCaptcha();
        var second = await FetchCaptcha();

        first.Image.Should().StartWith("data:image/png;base64,");
        first.Id.Should().NotBe(second.Id);
        first.Image.Should().NotBe(second.Image);
    }

    [Fact]
    public async Task Login_AfterThreshold_RequiresCaptcha_ThenPassesWithCode()
    {
        ResetGate();
        var username = "captcha_user01";
        await InsertAccount(username, UserTypes.AllowSelfService);

        // below threshold: wrong passwords surface as 401, not 428
        await Login(username, "wrong-pass", 4);
        await Login(username, "wrong-pass", 4);

        // gated: correct credentials without a captcha are rejected too
        (await PostLogin(username, "Passw0rd1")).HaveCode(428);

        // wrong captcha code → still 428
        var (id, _, _) = await FetchCaptcha();
        (await PostLogin(username, "Passw0rd1", id, "ZZZZZ")).HaveCode(428);

        // valid captcha + wrong password → 401 (captcha accepted, auth failed)
        (id, var code, _) = await FetchCaptcha();
        (await PostLogin(username, "wrong-pass", id, code)).HaveCode(401);

        // valid captcha + correct password → 200
        (id, code, _) = await FetchCaptcha();
        (await PostLogin(username, "Passw0rd1", id, code)).HaveCode(200);

        // success cleared the gate: the next wrong password is a plain 401
        (await PostLogin(username, "wrong-pass")).HaveCode(401);
    }

    [Fact]
    public async Task Captcha_IsSingleUse()
    {
        ResetGate();
        var username = "captcha_user02";
        await InsertAccount(username, UserTypes.AllowSelfService);
        await TripGate(username);

        // one captcha buys exactly one attempt — even a failed one
        var (id, code, _) = await FetchCaptcha();
        (await PostLogin(username, "wrong-pass", id, code)).HaveCode(401);

        (await PostLogin(username, "Passw0rd1", id, code)).HaveCode(428);
    }

    [Fact]
    public async Task AllFourAnonymousEndpoints_GateAfterThreshold_AndAcceptHeaders()
    {
        ResetGate();
        var username = "captcha_user03";
        await InsertAccount(username, UserTypes.AllowSelfService);
        await TripGate(username);

        // while gated, every anonymous auth endpoint rejects a bare request
        (await PostForgetPass($"{username}@gmail.com")).HaveCode(428);
        (await PostResetPass("whatever-code", "NewPassw0rd1")).HaveCode(428);
        (await PostRegister("captcha_ghost03")).HaveCode(428);
        (await PostLogin(username, "Passw0rd1")).HaveCode(428);

        // valid headers let each endpoint reach its own action logic — a
        // 400 outcome (expired reset code) still counts as a failure, so the
        // gate stays on for the next endpoint
        var (id, code, _) = await FetchCaptcha();
        (await PostResetPass("whatever-code", "NewPassw0rd1", id, code)).HaveCode(400);

        (id, code, _) = await FetchCaptcha();
        (await PostRegister("captcha_newuser03", id, code)).HaveCode(200);

        // the successful register cleared the gate — re-trip before proving
        // forget-pass consumes the headers too
        await TripGate(username);

        (id, code, _) = await FetchCaptcha();
        (await PostForgetPass($"{username}@gmail.com", id, code)).HaveCode(200);
    }

    private async Task TripGate(string username)
    {
        (await PostLogin(username, "wrong-pass")).HaveCode(401);
        (await PostLogin(username, "wrong-pass")).HaveCode(401);
        Cache.Get<int>(CaptchaService.FailureKey(GateKey())).Should().Be(Threshold);
    }

    private async Task<(string Id, string Code, string Image)> FetchCaptcha()
    {
        var response = await Client.GetAsync("/api/auth/captcha");
        var result = await CheckResponse<CaptchaResult>(response, 2);
        var captcha = result?.Data ?? throw new Exception("captcha data missing");

        var code = Cache.Get<string>(CaptchaService.CodeKey(captcha.Id))
            ?? throw new Exception("captcha answer missing from cache");

        return (captcha.Id, code, captcha.Image);
    }

    private Task<HttpResponseMessage> PostLogin(string username, string password,
        string? captcha_id = null, string? captcha_code = null)
    {
        return Post("/api/auth/token", new TokenContext { Username = username, Password = password },
            captcha_id, captcha_code);
    }

    private Task<HttpResponseMessage> PostRegister(string username,
        string? captcha_id = null, string? captcha_code = null)
    {
        // deterministic per-username mobile in the wide seed space — the
        // narrow +9891212345{xx} range belongs to other scenario fixtures
        var seed = Math.Abs(username.Aggregate(17, (current, c) => current * 31 + c));

        return Post("/api/auth/register", new RegisterModel
        {
            Firstname = "fname",
            Lastname = "lname",
            Mobile = $"+9893{seed % 100_000_000:D8}",
            Email = $"{username}@gmail.com",
            Username = username,
            Password = "Passw0rd1",
        }, captcha_id, captcha_code);
    }

    private Task<HttpResponseMessage> PostForgetPass(string email_mobile,
        string? captcha_id = null, string? captcha_code = null)
    {
        return Post("/api/auth/forget-pass", new ResetPasswordContext { EmailMobile = email_mobile },
            captcha_id, captcha_code);
    }

    private Task<HttpResponseMessage> PostResetPass(string token, string password,
        string? captcha_id = null, string? captcha_code = null)
    {
        return Post("/api/auth/reset-pass", new ChangePasswordContext { Token = token, Password = password },
            captcha_id, captcha_code);
    }

    private async Task<HttpResponseMessage> Post(string url, object body,
        string? captcha_id, string? captcha_code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };

        if (captcha_id != null && captcha_code != null)
        {
            request.Headers.Add(RequireCaptchaAttribute.IdHeader, captcha_id);
            request.Headers.Add(RequireCaptchaAttribute.CodeHeader, captcha_code);
        }

        return await Client.SendAsync(request);
    }

    /// <summary>
    /// The failure counter is per client IP; the test host has no socket so
    /// the gate key is the limiter's "unknown" fallback (defensively clear
    /// the loopback candidates too).
    /// </summary>
    private void ResetGate()
    {
        foreach (var candidate in GateKeyCandidates())
        {
            Cache.Remove(CaptchaService.FailureKey(candidate));
        }
    }

    private string GateKey() => GateKeyCandidates().First();

    private static IEnumerable<string> GateKeyCandidates() => ["unknown", "127.0.0.1", "::1"];

    public class CaptchaFactory : ProgramLevelInitializer.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // base disables captcha (shared-class contamination) — re-enable
            // with a low threshold so the gate trips after two failures
            builder.UseSetting("CaptchaOptions:Enabled", "true");
            builder.UseSetting("CaptchaOptions:FailureThreshold", Threshold.ToString());
        }
    }
}

file static class CaptchaResponseAssertions
{
    public static void HaveCode(this HttpResponseMessage response, short code)
    {
        var result = response.Content.ReadFromJsonAsync<ApiResult>(BodyOptions).GetAwaiter().GetResult();

        result.Should().NotBeNull($"body of {response.RequestMessage?.RequestUri} should be an ApiResult");
        result!.Code.Should().Be(code);
    }

    private static readonly JsonSerializerOptions BodyOptions = new() { PropertyNameCaseInsensitive = true };
}
