using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;

namespace PhotonBypass.Xui.WebService;

class XuiApiService(IHttpClientFactory factory) : IXuiApi
{
    private const string AjaxHeader = "XMLHttpRequest";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<int, string> session_cookies = [];

    public async Task<List<XuiClientModel>> ListClients(ServerEntity server)
    {
        var inbound = await GetManagedInbound(server);

        return ParseClients(inbound, server);
    }

    public async Task<List<XuiClientTrafficModel>> ListClientTraffic(ServerEntity server)
    {
        var inbound = await GetManagedInbound(server);

        return [.. (inbound.ClientStats ?? []).Select(stats => stats.ToModel())];
    }

    public async Task<List<XuiOnlineClientModel>> ListOnlineClients(ServerEntity server)
    {
        return await PostAsync<List<XuiOnlineClientModel>>(server, "xui/API/inbounds/onlines", null)
               ?? [];
    }

    public async Task<XuiClientModel?> GetClient(ServerEntity server, string email)
    {
        var clients = await ListClients(server);

        return clients.FirstOrDefault(client => client.Email == email);
    }

    public async Task<XuiInboundModel> GetInbound(ServerEntity server)
    {
        var inbound = await GetManagedInbound(server);

        return new XuiInboundModel
        {
            Id = inbound.Id,
            Port = inbound.Port,
            Protocol = inbound.Protocol ?? "",
            StreamSettings = inbound.StreamSettings,
        };
    }

    public async Task AddClient(ServerEntity server, XuiClientModel client)
    {
        await PostAsync(server, "xui/API/inbounds/addClient", BuildClientBody(server, client));
    }

    public async Task UpdateClient(ServerEntity server, string currentUuid, XuiClientModel client)
    {
        await PostAsync(server, $"xui/API/inbounds/updateClient/{currentUuid}", BuildClientBody(server, client));
    }

    public async Task RemoveClient(ServerEntity server, string uuid)
    {
        var inbound_id = GetConfiguredInboundId(server);

        await PostAsync(server, $"xui/API/inbounds/{inbound_id}/delClient/{uuid}", null);
    }

    public async Task ResetClientTraffic(ServerEntity server, string email)
    {
        var inbound_id = GetConfiguredInboundId(server);

        await PostAsync(server, $"xui/API/inbounds/{inbound_id}/resetClientTraffic/{email}", null);
    }

    private static object BuildClientBody(ServerEntity server, XuiClientModel client)
    {
        return new
        {
            id = GetConfiguredInboundId(server),
            settings = JsonSerializer.Serialize(new XuiSettingsBody { Clients = [client] }, WebJson),
        };
    }

    private static int GetConfiguredInboundId(ServerEntity server)
    {
        var config = GetWebApiConfig(server);

        return config.InboundId ?? throw new Exception(
            $"The x-ui inbound id is not configured for server ({server.Id}:{server.Name}); " +
            "WebApiConfig.InboundId must point at the inbound that owns the managed clients.");
    }

    private async Task<XuiInboundBody> GetManagedInbound(ServerEntity server)
    {
        var inbound_id = GetConfiguredInboundId(server);

        var inbounds = await ListInbounds(server);

        return inbounds.FirstOrDefault(inbound => inbound.Id == inbound_id)
               ?? throw new Exception(
                   $"The configured x-ui inbound ({inbound_id}) does not exist on server " +
                   $"({server.Id}:{server.Name}); available inbound ids: " +
                   $"[{string.Join(", ", inbounds.Select(inbound => inbound.Id))}].");
    }

    private static List<XuiClientModel> ParseClients(XuiInboundBody inbound, ServerEntity server)
    {
        if (string.IsNullOrWhiteSpace(inbound.Settings))
        {
            return [];
        }

        XuiSettingsBody? settings;

        try
        {
            settings = JsonSerializer.Deserialize<XuiSettingsBody>(inbound.Settings, WebJson);
        }
        catch (JsonException error)
        {
            throw new Exception(
                $"The x-ui inbound settings could not be parsed: (server={server.Name}, inbound-id={inbound.Id})",
                error);
        }

        return [.. settings?.Clients ?? []];
    }

    private async Task<List<XuiInboundBody>> ListInbounds(ServerEntity server)
    {
        var payload = await GetAsync<XuiInboundBody[]>(server, "xui/API/inbounds/");

        return [.. payload ?? []];
    }

    private async Task<T?> GetAsync<T>(ServerEntity server, string path)
    {
        using var response = await SendAsync(server, HttpMethod.Get, path, null);

        return await ReadEnvelopeAsync<T>(response, server, path);
    }

    private async Task PostAsync(ServerEntity server, string path, object? body)
    {
        using var response = await SendAsync(server, HttpMethod.Post, path, body);

        await ReadEnvelopeAsync<object>(response, server, path);
    }

    private async Task<T?> PostAsync<T>(ServerEntity server, string path, object? body)
    {
        using var response = await SendAsync(server, HttpMethod.Post, path, body);

        return await ReadEnvelopeAsync<T>(response, server, path);
    }

    private static async Task<T?> ReadEnvelopeAsync<T>(HttpResponseMessage response, ServerEntity server, string path)
    {
        var envelope = await response.Content.ReadFromJsonAsync<XuiResponseEnvelope<T>>()
                       ?? throw new Exception($"The x-ui response could not be parsed: (server={server.Name}, path={path})");

        if (!envelope.Success)
        {
            throw new Exception($"The x-ui request failed: (server={server.Name}, path={path}, message={envelope.Msg})");
        }

        return envelope.Object;
    }

    private async Task<HttpResponseMessage> SendAsync(ServerEntity server, HttpMethod method, string path, object? body,
        bool allow_relogin = true)
    {
        var config = GetWebApiConfig(server);
        var base_url = BuildBaseUrl(config);

        var http = factory.CreateClient(ServiceFactory.HttpClientKeyName);

        if (!session_cookies.TryGetValue(server.Id, out var cookie))
        {
            cookie = await LoginAsync(http, base_url, config);
            session_cookies[server.Id] = cookie;
        }

        using var request = new HttpRequestMessage(method, base_url + path);

        request.Headers.TryAddWithoutValidation("X-Requested-With", AjaxHeader);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            session_cookies.Remove(server.Id);

            if (!allow_relogin)
            {
                throw new Exception($"The x-ui session was rejected after a re-login: (server={server.Name})");
            }

            return await SendAsync(server, method, path, body, allow_relogin: false);
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

        using var response = await http.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<XuiResponseEnvelope<object>>();

        if (envelope is not { Success: true })
        {
            throw new Exception(
                $"The x-ui login failed: (host={config.HostName}, message={envelope?.Msg ?? "unparseable response"})");
        }

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
        var base_path = NormalizeBasePath(config.BasePath);

        return $"{scheme}://{config.HostName}{port}{base_path}";
    }

    private static string NormalizeBasePath(string? base_path)
    {
        var path = string.IsNullOrEmpty(base_path) ? "/" : base_path.Trim();

        if (!path.StartsWith('/')) path = "/" + path;
        if (!path.EndsWith('/')) path += "/";

        return path;
    }

    private sealed class XuiResponseEnvelope<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("obj")]
        public T? Object { get; set; }
    }

    private sealed class XuiSettingsBody
    {
        [JsonPropertyName("clients")]
        public List<XuiClientModel>? Clients { get; set; }
    }

    private sealed class XuiInboundBody
    {
        public int Id { get; set; }

        public int Port { get; set; }

        public string? Protocol { get; set; }

        public string? StreamSettings { get; set; }

        public string? Settings { get; set; }

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
