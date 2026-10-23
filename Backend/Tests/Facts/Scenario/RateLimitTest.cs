using Microsoft.AspNetCore.Hosting;
using PhotonBypass.Test.Initializer;
using System.Net;
using System.Net.Http.Json;

namespace PhotonBypass.Test.Facts.Scenario;

public class RateLimitTest(RateLimitTest.LowRateFactory factory)
    : HttpHandler(factory), IClassFixture<RateLimitTest.LowRateFactory>
{
    private const int PermitLimit = 1;

    [Fact]
    public async Task AuthEndpoint_ThirdRequestWithinWindow_Gets429()
    {
        var username = "rate_user01";
        await Register(username, "61");
        await Login(username, "Passw0rd1");

        // the login above consumed the single auth permit: any further token
        // attempt inside the window is throttled
        var third = await Client.PostAsJsonAsync("/api/auth/token",
            new { Username = username, Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    public class LowRateFactory : ProgramLevelInitializer.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // one permit per policy per window: the endpoint buckets are
            // separate (register ≠ auth), so each helper call survives but
            // the very next hit on the same policy is throttled
            foreach (var policy in PhotonBypass.Portal.ServiceFactory.AllRateLimitPolicies)
            {
                builder.UseSetting($"RateLimitOptions:{policy}:PermitLimit", PermitLimit.ToString());
                builder.UseSetting($"RateLimitOptions:{policy}:WindowSeconds", "60");
            }
        }
    }
}

/// <summary>
/// docs/14 M34: the password-revalidating endpoints (vpn/change-ovpn,
/// account/change-pass) must sit behind the shared auth rate-limit policy —
/// without the attribute they form an unthrottled brute-force oracle for the
/// portal password. Separate class → separate app instance → a fresh limiter
/// bucket for the fixture.
/// </summary>
public class PasswordEndpointRateLimitTest(RateLimitTest.LowRateFactory factory)
    : HttpHandler(factory), IClassFixture<RateLimitTest.LowRateFactory>
{
    [Fact]
    public async Task ChangeOvpnPassword_RepeatedWrongTokens_Gets429()
    {
        // unique mobile suffix — scenario classes run in parallel against the
        // shared scenario database, a duplicate mobile fails registration
        var username = "rate_user02";
        await Register(username, "99");
        await Login(username, "Passw0rd1");

        // register + login each consumed their policy's single permit; the
        // next password verification attempt on change-ovpn hits the same
        // exhausted auth bucket and must be throttled too
        var third = await Client.PostAsJsonAsync("/api/vpn/change-ovpn", new
        {
            Target = (string?)null,
            Token = "wrong-token",
            Password = "NewPassw0rd1",
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
