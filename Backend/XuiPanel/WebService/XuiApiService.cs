using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;

namespace PhotonBypass.Xui.WebService;

class XuiApiService(IHttpClientFactory factory) : IXuiApi
{
    private readonly Dictionary<int, string> session_cookies = [];

    public async Task<List<XuiClientModel>> ListClients(ServerEntity server)
    {
        var inbounds = await ListInbounds(server);

        return inbounds.SelectMany(inbound => inbound.Clients ?? [])
            .Select(client => client.ToModel())
            .ToList();
    }

    public async Task<List<XuiClientTrafficModel>> ListClientTraffic(ServerEntity server)
    {
        var inbounds = await ListInbounds(server);

        return inbounds.SelectMany(inbound => inbound.ClientStats ?? [])
            .Select(stats => stats.ToModel())
            .ToList();
    }

    public async Task<XuiClientModel?> GetClient(ServerEntity server, string email)
    {
        var clients = await ListClients(server);

        return clients.FirstOrDefault(client => client.Email == email);
    }

    public async Task AddClient(ServerEntity server, XuiClientModel client)
    {
        var inbound_id = await GetManagedInboundId(server);

        await PostAsync(server, "panel/api/inbounds/addClient", new
        {
            id = inbound_id,
            settings = SerializeSettings(client),
        });
    }

    public async Task UpdateClient(ServerEntity server, XuiClientModel client)
    {
        var inbound_id = await GetManagedInboundId(server);

        await PostAsync(server, $"panel/api/inbounds/updateClient/{client.Email}", new
        {
            id = inbound_id,
            settings = SerializeSettings(client),
        });
    }

    public Task RemoveClient(ServerEntity server, string email)
    {
        return PostWithManagedInboundAsync(server,
            inbound_id => $"panel/api/inbounds/{inbound_id}/delClient/{email}");
    }

    public Task ResetClientTraffic(ServerEntity server, string email)
    {
        return PostWithManagedInboundAsync(server,
            inbound_id => $"panel/api/inbounds/{inbound_id}/resetClientTraffic/{email}");
    }

    private static string SerializeSettings(XuiClientModel client)
    {
        return JsonSerializer.Serialize(new XuiSettingsBody { Clients = [XuiClientBody.FromModel(client)] });
    }

    private async Task PostWithManagedInboundAsync(ServerEntity server, Func<string, string> build_path)
    {
        var inbound_id = await GetManagedInboundId(server);

        await PostAsync(server, build_path(inbound_id), null);
    }

    private async Task<string> GetManagedInboundId(ServerEntity server)
    {
        var inbounds = await ListInbounds(server);

        return inbounds.FirstOrDefault()?.Id ??
               throw new Exception($"No x-ui inbound is configured on server ({server.Id}:{server.Name}).");
    }

    private async Task<List<XuiInboundResponse>> ListInbounds(ServerEntity server)
    {
        var payload = await PostAsync<XuiInboundResponse[]>(server, "panel/api/inbounds/list", null);

        return [.. payload ?? []];
    }

    private async Task<T?> PostAsync<T>(ServerEntity server, string path, object? body)
    {
        var response = await SendAsync(server, path, body);

        var envelope = await response.Content.ReadFromJsonAsync<XuiResponseEnvelope<T>>()
                       ?? throw new Exception($"The x-ui response could not be parsed: (server={server.Name}, path={path})");

        if (!envelope.Success)
        {
            throw new Exception($"The x-ui request failed: (server={server.Name}, path={path}, message={envelope.Message})");
        }

        return envelope.Object;
    }

    private async Task PostAsync(ServerEntity server, string path, object? body)
    {
        var response = await SendAsync(server, path, body);

        var envelope = await response.Content.ReadFromJsonAsync<XuiResponseEnvelope<object>>()
                       ?? throw new Exception($"The x-ui response could not be parsed: (server={server.Name}, path={path})");

        if (!envelope.Success)
        {
            throw new Exception($"The x-ui request failed: (server={server.Name}, path={path}, message={envelope.Message})");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(ServerEntity server, string path, object? body)
    {
        var config = GetWebApiConfig(server);
        var base_url = BuildBaseUrl(config);

        var http = factory.CreateClient(ServiceFactory.HttpClientKeyName);

        using var request = new HttpRequestMessage(HttpMethod.Post, base_url + path);

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (!session_cookies.TryGetValue(server.Id, out var cookie))
        {
            cookie = await LoginAsync(http, base_url, config);
            session_cookies[server.Id] = cookie;
        }

        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await http.SendAsync(request);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            session_cookies.Remove(server.Id);

            throw new Exception($"The x-ui session was rejected: (server={server.Name})");
        }

        response.EnsureSuccessStatusCode();

        return response;
    }

    private static WebApiConfig GetWebApiConfig(ServerEntity server)
    {
        var config = server.Config?.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        if (string.IsNullOrEmpty(config.HostName))
        {
            throw new Exception($"The host-name configuration is not set for server ({server.Id}:{server.Name})");
        }

        return config;
    }

    private static async Task<string> LoginAsync(HttpClient http, string base_url, WebApiConfig config)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{base_url}login")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { username = config.Username, password = config.Password }),
                Encoding.UTF8, "application/json"),
        };

        var response = await http.SendAsync(request);

        response.EnsureSuccessStatusCode();

        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            throw new Exception("The x-ui login response carried no session cookie.");
        }

        return string.Join("; ", cookies.Select(cookie => cookie.Split(';').First()));
    }

    private static string BuildBaseUrl(WebApiConfig config)
    {
        var scheme = config.Ssl ? "https" : "http";
        var port = config.Port.HasValue ? $":{config.Port}" : "";

        return $"{scheme}://{config.HostName}{port}/";
    }

    private sealed class XuiResponseEnvelope<T>
    {
        public bool Success { get; set; }

        public string? Message { get; set; }

        public T? Object { get; set; }
    }

    private sealed class XuiSettingsBody
    {
        public List<XuiClientBody> Clients { get; set; } = [];
    }

    private sealed class XuiClientBody
    {
        public string Email { get; set; } = null!;

        public string Id { get; set; } = null!;

        public bool Enable { get; set; }

        public long? TotalTrafficBytes { get; set; }

        public long? ExpiryTime { get; set; }

        public int LimitIp { get; set; }

        public XuiClientModel ToModel()
        {
            return new XuiClientModel
            {
                Email = Email,
                Uuid = Id,
                Enable = Enable,
                TotalTrafficBytes = TotalTrafficBytes,
                ExpiryTime = ExpiryTime,
                LimitIp = LimitIp,
            };
        }

        public static XuiClientBody FromModel(XuiClientModel model)
        {
            return new XuiClientBody
            {
                Email = model.Email,
                Id = model.Uuid,
                Enable = model.Enable,
                TotalTrafficBytes = model.TotalTrafficBytes,
                ExpiryTime = model.ExpiryTime,
                LimitIp = model.LimitIp,
            };
        }
    }

    private sealed class XuiInboundResponse
    {
        public string Id { get; set; } = null!;

        public List<XuiClientBody>? Clients { get; set; }

        public List<XuiClientStatsBody>? ClientStats { get; set; }
    }

    private sealed class XuiClientStatsBody
    {
        public string Email { get; set; } = null!;

        public long Up { get; set; }

        public long Down { get; set; }

        public XuiClientTrafficModel ToModel()
        {
            return new XuiClientTrafficModel { Email = Email, Up = Up, Down = Down };
        }
    }
}
