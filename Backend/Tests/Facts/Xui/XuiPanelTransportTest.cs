using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Live transport contract of the alireza0/x-ui panel on the Xui-Test VM
/// (Docs/23 D4/D5): the login envelope + session cookie, and the 307/401
/// split keyed by the X-Requested-With header. The client-side retry logic
/// stays mock-covered (XuiApiTransportTest); this tier pins the panel half
/// of the contract.
/// </summary>
[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelTransportTest : OutSourceLevelServiceInitializer
{
    private static string BaseUrl => $"http://{XuiPanelTestServer.Vm.Ip}:{XuiPanelTestServer.Vm.Port}";

    [Fact]
    public async Task Login_ValidCredentials_ReturnsSuccessEnvelopeAndUiSessionCookie()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        using var http = new HttpClient();

        using var response = await http.PostAsync($"{BaseUrl}/login", LoginBody(
            XuiPanelTestServer.Vm.Username, XuiPanelTestServer.Vm.Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("success").GetBoolean().Should().BeTrue();

        response.Headers.GetValues("Set-Cookie").Should().Contain(cookie =>
            cookie.StartsWith("x-ui=") && cookie.Contains("Path="));
    }

    [Fact]
    public async Task Login_WrongCredentials_Http200WithFailureEnvelope()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        using var http = new HttpClient();

        using var response = await http.PostAsync($"{BaseUrl}/login", LoginBody(
            XuiPanelTestServer.Vm.Username, "definitely-wrong"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ApiWithStaleCookie_WithoutAjaxHeader_RedirectsToLoginInsteadOf401()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/xui/API/inbounds/");
        request.Headers.TryAddWithoutValidation("Cookie", "x-ui=stale-session-value");

        using var response = await http.SendAsync(request);

        // the redirect lands on the web root (the SPA login view), not on a
        // literal /login path — verified live on v1.14.0
        response.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task ApiWithStaleCookieAndAjaxHeader_Gets401JsonEnvelope()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, this);

        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/xui/API/inbounds/");
        request.Headers.TryAddWithoutValidation("Cookie", "x-ui=stale-session-value");
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        using var response = await http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("success").GetBoolean().Should().BeFalse();
    }

    private static StringContent LoginBody(string username, string password)
    {
        return new StringContent(
            JsonSerializer.Serialize(new { username, password }),
            Encoding.UTF8, "application/json");
    }
}
