using System.Net.Http.Json;
using System.Web;
using OutSource.ApiResponseModel;
using PhotonBypass.Domain.Radius;

namespace PhotonBypass.OutSource;

public class RadiusDesk : IDisposable
{
    private readonly HttpClient httpClient;
    private string? token;

    private const string SEL_LANGUAGE = "4_4";

    public RadiusDesk()
    {
        httpClient = new HttpClient
        {
            BaseAddress = new Uri("/cake4/rd_cake")
        };
    }

    public PermenantUserEntity? GetPermenentUser(string username)
    {
        CheckLogin();

        var url = $"permanent-users/index.json?_dc={GetTime()}&page=1&start=0&limit=2&sort=username&dir=ASC";

        url += $"&token={token}&sel_language={SEL_LANGUAGE}&filter=";
        url += HttpUtility.UrlEncode($"[{{\"operator\":\"==\",\"value\":true,\"property\":\"active\"}},{{\"operator\":\"==\",\"value\":\"{username}\",\"property\": \"username\"}}]");

        var httpMessage = httpClient.GetAsync(url).Result
            .EnsureSuccessStatusCode();

        if (httpMessage == null) return null;

        var result = httpMessage.Content.ReadFromJsonAsync<PermanentUsersResponse>().Result;

        if (result?.TotalCount != 1) return null;

        return result.Items?[0];
    }

    public void Dispose()
    {
        Logout();
        GC.SuppressFinalize(this);
    }

    private long GetTime()
    {
        return 0;
    }

    private void CheckLogin()
    {
        if (token == null || CheckToken())
        {
            var success = Login();

            if (!success)
            {
                throw new Exception("Cannot login to radius");
            }
        }
    }

    private bool Login()
    {
        var loginData = new
        {
            auto_compact = false,
            username = "user",
            password = "pass"
        };

        var response = httpClient.PostAsJsonAsync<object>("dashboard/authenticate.json", loginData).Result
            .EnsureSuccessStatusCode();

        if (response == null) return false;

        var result = response.Content.ReadFromJsonAsync<dynamic>().Result;

        token = result?.data.token;

        return token != null;
    }

    private bool CheckToken()
    {
        var tokenData = new
        {
            _dc = 1740571551428,
            token = "01831f18-6691-4c39-af82-5803aa885c0c",
            auto_compact = false
        };

        var response = httpClient.PostAsJsonAsync<object>("dashboard/check_token.json", tokenData).Result
            .EnsureSuccessStatusCode();

        if (response == null) return false;

        var result = response.Content.ReadFromJsonAsync<dynamic>().Result;

        return result?.success == true;
    }

    private void Logout()
    {
        httpClient.GetAsync("dashboard/branding.json").Result
            .EnsureSuccessStatusCode();

        token = null;
    }
}
