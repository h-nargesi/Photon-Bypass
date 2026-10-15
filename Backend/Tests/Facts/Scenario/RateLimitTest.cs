using Microsoft.AspNetCore.Hosting;
using PhotonBypass.Test.Initializer;
using System.Net;
using System.Net.Http.Json;

namespace PhotonBypass.Test.Facts.Scenario;

public class RateLimitTest(RateLimitTest.LowRateFactory factory)
    : HttpHandler(factory), IClassFixture<RateLimitTest.LowRateFactory>
{
    private const int PermitLimit = 2;

    [Fact]
    public async Task AuthEndpoint_ThirdRequestWithinWindow_Gets429()
    {
        // register uses the rate-limited auth controller too, so count it in
        var username = "rate_user01";
        await Register(username, "61");
        await Login(username, "Password");

        var third = await Client.PostAsJsonAsync("/api/auth/token",
            new { Username = username, Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    public class LowRateFactory : ProgramLevelInitializer.Factory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseSetting("RateLimitOptions:PermitLimit", PermitLimit.ToString());
            builder.UseSetting("RateLimitOptions:WindowSeconds", "60");
        }
    }
}
