using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Xui.WebService;

class XuiApiService(IXuiHttpClientProvider provider) : IXuiApi
{
    private const string AjaxHeader = "XMLHttpRequest";

    private const string ListInboundsPath = "xui/API/inbounds/";

    private const string ListOnlinesPath = "xui/API/inbounds/onlines";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<int, string> session_cookies = new();

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
        return await PostAsync<List<XuiOnlineClientModel>>(server, ListOnlinesPath, null)
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
        var payload = await GetAsync<XuiInboundBody[]>(server, ListInboundsPath);

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

    private async Task<HttpResponseMessage> SendAsync(ServerEntity server, HttpMethod method, string path, object? body)
    {
        // Config decrypts and parses on every access: read it once per call
        var (configuration, config) = RequireWebConfiguration(server);

        var base_url = XuiLogin.BaseUrl(config);

        var http = provider.Get(server);

        var budget = new SendBudget(
            transport_retryable: configuration.Transport == ServerTransport.SshTunnel && IsTransportRetryable(path));

        while (true)
        {
            // a send without a session cookie needs two: the login and the request itself
            if (budget.SendsLeft < Needed(session_cookies.ContainsKey(server.Id)))
            {
                throw new Exception(
                    $"The x-ui request ran out of its send budget ({SendBudget.MaximumSends}): (server={server.Name}, path={path})");
            }

            HttpResponseMessage response;

            try
            {
                response = await SendOnceAsync(http, server, base_url, config, method, path, body, budget);
            }
            catch (Exception error) when (budget.TransportRetriesLeft > 0 &&
                                          budget.SendsLeft >= Needed(session_cookies.ContainsKey(server.Id)) &&
                                          XuiTransportDeath.Is(error))
            {
                budget.TransportRetriesLeft--;

                // the pooled tunnel session is rebuilt before the request is sent again
                provider.ReportTransportFailure(server);

                continue;
            }

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                response.EnsureSuccessStatusCode();

                return response;
            }

            response.Dispose();

            session_cookies.TryRemove(server.Id, out _);

            if (budget.ReloginsLeft-- < 1)
            {
                throw new Exception($"The x-ui session was rejected after a re-login: (server={server.Name})");
            }
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpClient http, ServerEntity server, string base_url, WebApiConfig config,
        HttpMethod method, string path, object? body, SendBudget budget)
    {
        if (!session_cookies.TryGetValue(server.Id, out var cookie))
        {
            // counted when it leaves: a send that dies on the wire is a send too
            budget.SendsLeft--;

            cookie = await XuiLogin.LoginAsync(http, base_url, config);

            session_cookies[server.Id] = cookie;
        }

        using var request = new HttpRequestMessage(method, base_url + path);

        request.Headers.TryAddWithoutValidation("X-Requested-With", AjaxHeader);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        budget.SendsLeft--;

        return await http.SendAsync(request);
    }

    private static int Needed(bool has_cookie) => has_cookie ? 1 : 2;

    /// <summary>
    /// Reads and the login are safe to send again after the tunnel died;
    /// client mutations are not (Docs/25 D7).
    /// </summary>
    private static bool IsTransportRetryable(string path) =>
        path == ListInboundsPath || path == ListOnlinesPath || path == XuiLogin.Path;

    private static WebApiConfig GetWebApiConfig(ServerEntity server) => RequireWebConfiguration(server).Config;

    private static (ServerConfiguration Configuration, WebApiConfig Config) RequireWebConfiguration(ServerEntity server)
    {
        var configuration = server.Config ??
                            throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        var config = configuration.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        if (string.IsNullOrEmpty(config.HostName))
        {
            throw new Exception($"The host-name configuration is not set for server ({server.Id}:{server.Name})");
        }

        return (configuration, config);
    }

    /// <summary>
    /// Sends left for one logical request. Five is the exact ceiling of the
    /// retry rules: a request that starts without a session cookie needs the
    /// login and the request, one transport retry (with its own login when the
    /// first one died) and one re-login with its request. The budget itself is
    /// only a guard: it ends the request when the rules would ever need more.
    /// </summary>
    private sealed class SendBudget(bool transport_retryable)
    {
        public const int MaximumSends = 5;

        public int SendsLeft { get; set; } = MaximumSends;

        public int ReloginsLeft { get; set; } = 1;

        public int TransportRetriesLeft { get; set; } = transport_retryable ? 1 : 0;
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
