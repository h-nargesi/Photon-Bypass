using System.Net.Http.Json;

namespace PhotonBypass.OutSource;

public class RadiusDesk : IDisposable
{
    private readonly HttpClient httpClient;
    private string? token;

    public RadiusDesk()
    {
        httpClient = new HttpClient
        {
            BaseAddress = new Uri("/cake4/rd_cake")
        };
    }

    public void GetPermenentUser()
    {
        if (token == null)
        {
            Login();
        }
    }

    public void Dispose() => Logout();

    private void Login()
    {
        var loginData = new
        {
            auto_compact = false,
            username = "user",
            password = "pass"
        };

        var httpMessage = httpClient.PostAsJsonAsync<object>("dashboard/authenticate.json", loginData).Result
            .EnsureSuccessStatusCode();

        if (httpMessage == null) return;

        var result = httpMessage.Content.ReadFromJsonAsync<dynamic>().Result;

        token = result?.data.token;
    }

    private void CheckToken()
    {
        var tokenData = new
        {
            _dc = 1740571551428,
            token = "01831f18-6691-4c39-af82-5803aa885c0c",
            auto_compact = false
        };

        var httpMessage = httpClient.PostAsJsonAsync<object>("dashboard/check_token.json", tokenData).Result
            .EnsureSuccessStatusCode();

        if (httpMessage == null) return;

        var result = httpMessage.Content.ReadFromJsonAsync<dynamic>().Result;
    }

    private void Logout()
    {
        httpClient.GetAsync("dashboard/branding.json").Result
            .EnsureSuccessStatusCode();

        token = null;
    }
}
