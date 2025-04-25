using System.Net.Http.Json;
using System.Text;
using System.Web;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Radius.WebService.ApiResponseModel;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.WebService;

class RadiusDeskService : IRadiusService, IDisposable
{
    private readonly RadiusServiceOptions options;
    private readonly HttpClient httpClient;
    private string? token;
    private DateTime lastRequest = DateTime.Now;

    private const string SEL_LANGUAGE = "4_4";

    public RadiusDeskService(IOptions<RadiusServiceOptions> options)
    {
        this.options = options.Value;

        httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{this.options.BaseUrl}/cake4/rd_cake")
        };
    }

    public async Task<PermanentUserEntity?> GetPermenentUser(string username)
    {
        await CheckLogin();

        var url = $"permanent-users/index.json?_dc={GetTime()}&page=1&start=0&limit=2&sort=username&dir=ASC";

        url += $"&token={token}&sel_language={SEL_LANGUAGE}&filter=";
        url += HttpUtility.UrlEncode($"[{{\"operator\":\"==\",\"value\":true,\"property\":\"active\"}},{{\"operator\":\"==\",\"value\":\"{username}\",\"property\": \"username\"}}]");

        var httpMessage = httpClient.GetAsync(url).Result
            .EnsureSuccessStatusCode();

        if (httpMessage == null) return null;

        var result = await httpMessage.Content.ReadFromJsonAsync<PermanentUsersResponse>();

        if (result?.TotalCount != 1) return null;

        return result.Items?[0];
    }

    public async Task<bool> ActivePermanentUser(int user_id, int cloud_id, bool active)
    {
        await CheckLogin();

        var data = new Dictionary<string, object?>
        {
            {"rb", active },
            {"token", token },
            {"cloud_id", cloud_id },
            {"sel_language", SEL_LANGUAGE },
        };

        var response = await PostAsync<object, dynamic>("permanent-users/enable-disable.json", data);

        return response?.success ?? false;
    }

    public Task<string> GetOvpnPassword(int user_id)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ChangeOvpnPassword(string username, string password)
    {
        throw new NotImplementedException();
    }

    public Task SavePermenentUser(PermanentUserEntity user)
    {
        throw new NotImplementedException();
    }

    public Task<IList<TrafficDataRadius>> FetchTrafficData(DateTime index, TrafficDataRequestType type)
    {
        throw new NotImplementedException();
    }

    public Task SetRestrictedServer(int user_id, string? server_ip)
    {
        throw new NotImplementedException();
    }

    public Task UpdateUserDataUsege(int user_id)
    {
        throw new NotImplementedException();
    }

    public Task SetUserDate(int user_id, DateTime from, DateTime to)
    {
        throw new NotImplementedException();
    }

    public Task InsertTopUpAndMakeActive(string target, PlanType type, int value)
    {
        throw new NotImplementedException();
    }

    public void Dispose() => Logout();

    private static long GetTime()
    {
        return UnixTimestampConverter.DateTimeToUnixTimeStamp(DateTime.Now);
    }

    private async Task CheckLogin()
    {
        if (token == null || await CheckToken())
        {
            var success = await Login();

            if (!success)
            {
                throw new Exception("Cannot login to radius");
            }
        }
    }

    private async Task<bool> Login()
    {
        var loginData = new
        {
            auto_compact = false,
            username = options.Username,
            password = options.Password
        };

        var response = await PostAsync<object, dynamic>("dashboard/authenticate.json", loginData);

        token = response?.data.token;

        return token != null;
    }

    private async Task<bool> CheckToken()
    {
        var tokenData = new
        {
            _dc = GetTime(),
            token,
            auto_compact = false
        };

        var response = await GetAsync<object, dynamic>("dashboard/check_token.json", tokenData);

        return response?.success == true;
    }

    private Task<HttpResponseMessage> Logout()
    {
        token = null;

        return GetAsync("dashboard/branding.json");
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string url, T data)
    {
        var response = await httpClient.PostAsync(url, FormContent(data));

        return response.EnsureSuccessStatusCode();
    }

    private async Task<R?> PostAsync<T, R>(string url, T data)
    {
        var response = await PostAsync(url, data);

        return await response.Content.ReadFromJsonAsync<R>();
    }

    private async Task<HttpResponseMessage> GetAsync(string url)
    {
        var response = await httpClient.GetAsync(url);

        return response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> GetAsync<T>(string url, T data)
    {
        return GetAsync(url + QueryString(data));
    }

    private async Task<R?> GetAsync<R>(string url)
    {
        var response = await GetAsync(url);

        return await response.Content.ReadFromJsonAsync<R>();
    }

    private async Task<R?> GetAsync<T, R>(string url, T data)
    {
        var response = await GetAsync(url, data);

        return await response.Content.ReadFromJsonAsync<R>();
    }

    private static FormUrlEncodedContent FormContent<T>(T data)
    {
        var keyValues = new List<KeyValuePair<string, string?>>();

        if (data is IDictionary<string, object?> dict)
        {
            foreach (var item in dict)
            {
                keyValues.Add(new KeyValuePair<string, string?>(item.Key, item.Value?.ToString()));
            }
        }
        else
        {
            foreach (var item in typeof(T).GetProperties())
            {
                var value = item.GetValue(data);
                keyValues.Add(new KeyValuePair<string, string?>(item.Name, value?.ToString()));
            }
        }

        return new FormUrlEncodedContent(keyValues);
    }

    private static string QueryString<T>(T data)
    {
        var result = HttpUtility.ParseQueryString(string.Empty);
        foreach (var item in typeof(T).GetProperties())
        {
            var value = item.GetValue(data);
            if (value == null) continue;

            result[item.Name] = value.ToString();
        }

        if (result.Count < 1) return string.Empty;

        return "?" + result.ToString();
    }

    private static string QueryStringStringBuilder<T>(T data)
    {
        var result = new StringBuilder();
        foreach (var item in typeof(T).GetProperties())
        {
            var value = item.GetValue(data);
            if (value == null) continue;

            result.Append('$')
                .Append(HttpUtility.UrlEncode(item.Name))
                .Append('=')
                .Append(HttpUtility.UrlEncode(value.ToString()));
        }

        if (result.Length < 1) return string.Empty;

        return result.Remove(0, 1).Insert(0, '?').ToString();
    }
}
