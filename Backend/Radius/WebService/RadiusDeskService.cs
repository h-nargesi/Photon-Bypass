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
    private const int TIMEZONE_ID = 262;

    public RadiusDeskService(IOptions<RadiusServiceOptions> options)
    {
        this.options = options.Value;

        httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{this.options.BaseUrl}/cake4/rd_cake")
        };
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

    public async Task<string?> GetOvpnPassword(int user_id, int cloud_id)
    {
        await CheckLogin();

        var data = new
        {
            _dc = GetTime(),
            token,
            user_id,
            cloud_id,
            sel_language = SEL_LANGUAGE
        };

        var response = await GetAsync<object, dynamic>("permanent-users/view-password.json", data);

        return response?.value ?? null;
    }

    public async Task<bool> ChangeOvpnPassword(int user_id, string password)
    {
        await CheckLogin();

        var data = new
        {
            _dc = GetTime(),
            token,
            user_id,
            password,
            sel_language = SEL_LANGUAGE
        };

        var response = await PostAsync<object, dynamic>("permanent-users/change-password.json", data);

        return response?.success ?? false;
    }

    public async Task<bool> SavePermenentUser(PermanentUserEntity user)
    {
        var data = new
        {
            id = user.Id,
            token,
            realm_id = user.RealmId,
            profile_id = user.ProfileId,
            time_cap_type = "hard",
            data_cap_type = "hard",
        };

        var response = await PostAsync<object, dynamic>("permanent-users/edit-basic-info.json", data);

        return response?.success ?? false;
    }

    public async Task<bool> RegisterPermenentUser(PermanentUserEntity user, string password)
    {
        await CheckLogin();

        var data = new
        {
            username = user.Username,
            password,
            realm_id = user.RealmId,
            profile_id = user.ProfileId,
            name = user.Name,
            surname = user.Surname,
            language = SEL_LANGUAGE,
            phone = user.Phone,
            email = user.Email,
            always_active = "always_active",
            realm_vlan_id = 0,
            sel_language = SEL_LANGUAGE,
            cloud_id = user.CloudId,
            token,
        };

        var response = await PostAsync<object, dynamic>("permanent-users/add.json", data);

        var success = response?.success ?? false;

        if (!success)
        {
            return success;
        }

        var reload = await GetPermenentUser(user.Username);

        if (reload != null)
        {
            user.Id = reload.Id;
        }

        return success;
    }

    public async Task<TrafficDataRadius[]> FetchTrafficData(string username, DateTime index, TrafficDataRequestType type)
    {
        await CheckLogin();

        var data = new
        {
            _dc = GetTime(),
            username,
            type = "permanent",
            span = type.ToString().ToLower(),
            timezone_id = TIMEZONE_ID,
            page = 1,
            start = 0,
            limit = 25,
            token,
            sel_language = SEL_LANGUAGE,
        };

        var response = await PostAsync<object, dynamic>("user-stats/index.json", data);

        if (response?.success != true)
        {
            throw new Exception("Error loading data from 'user-stats/index.json'");
        }

        return response.items as TrafficDataRadius[];
    }

    public Task<bool> SetRestrictedServer(int user_id, string? server_ip)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UpdateUserDataUsege(int user_id)
    {
        throw new NotImplementedException();
    }

    public Task<bool> SetUserDate(int user_id, DateTime from, DateTime to)
    {
        throw new NotImplementedException();
    }

    public Task<bool> InsertTopUpAndMakeActive(string target, PlanType type, int value)
    {
        throw new NotImplementedException();
    }

    public void Dispose() => Logout();

    private static long GetTime()
    {
        return UnixTimestampConverter.DateTimeToUnixTimeStamp(DateTime.Now);
    }

    private async Task<PermanentUserEntity?> GetPermenentUser(string username)
    {
        await CheckLogin();

        var filters = $"[{{\"operator\":\"==\",\"value\":true,\"property\":\"active\"}},{{\"operator\":\"==\",\"value\":\"{username}\",\"property\": \"username\"}}]";

        var data = new
        {
            _dc = GetTime(),
            page = 1,
            start = 0,
            limit = 2,
            sort = username,
            dir = "ASC",
            token,
            sel_language = SEL_LANGUAGE,
            filter = "[{\"operator\":\"==\",\"value\":true,\"property\":\"active\"},{\"operator\":\"==\",\"value\":\"U0171@ry\",\"property\": \"username\"}]",
        };

        var response = await PostAsync<object, PermanentUsersResponse>("permanent-users/index.json", data);

        return response?.Items?[0];
    }

    private async Task<bool> EditPersonalInfo(PermanentUserEntity user)
    {
        var data = new
        {
            id = user.Id,
            name = user.Name,
            surname = user.Surname,
            phone = user.Phone,
            email = user.Email,
            token,
        };

        var response = await PostAsync<object, dynamic>("permanent-users/edit-personal-info.json", data);

        return response?.success ?? false;
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
        var now = DateTime.Now;
        if ((now - lastRequest).Minutes < 5) return true;
        else lastRequest = now;

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
