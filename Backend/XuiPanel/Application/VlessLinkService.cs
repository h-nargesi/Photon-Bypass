using System.Text.Json;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Xui.Application;

/// <summary>
/// vless link builder for x-ui data-planes (docs/16 D4). Transport parameters
/// (address/port/protocol/stream) are read live from the panel; the local
/// server row only contributes the connection address. The stream-settings
/// parser accepts both the camelCase keys of the x-ui payload and the
/// PascalCase variants seen in exported configs (case-insensitive binding).
/// </summary>
public class VlessLinkService(
    Lazy<IServerRepository> server_repo,
    Lazy<IXuiApi> api) : IVpnLinkService
{
    private static readonly JsonSerializerOptions StreamJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] SupportedNetworks = ["tcp", "ws", "grpc"];

    private static readonly string[] SupportedSecurities = ["none", "tls", "reality"];

    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;

    private Lazy<IXuiApi> Api { get; } = api;

    public async Task<string> GetVlessLink(int? realm_id, string username)
    {
        // One x-ui server per realm is the operational invariant (docs/17 §3);
        // the collector applies the same first-of-realm selection.
        var server = (await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id))
            .FirstOrDefault(server => server.GetSyncKey() == RadiusType.Xui)
            ?? throw new Exception($"No x-ui server found for realm-id: ({realm_id})");

        // Keyed by the UUID currently live in the panel: the link must reflect
        // what is actually installed, not the (possibly drifted) database copy.
        var client = await Api.Value.GetClient(server, username)
                  ?? throw new Exception($"The x-ui client was not found: (server={server.Name}, user={username})");

        var inbound = await Api.Value.GetInbound(server);

        return Build(server, inbound, client.Uuid, username);
    }

    public static string Build(ServerEntity server, XuiInboundModel inbound, string uuid, string label)
    {
        if (!string.Equals(inbound.Protocol, "vless", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                $"The managed x-ui inbound is not a vless inbound: (server={server.Name}, inbound-id={inbound.Id}, protocol={inbound.Protocol})");
        }

        if (inbound.Port is < 1 or > 65535)
        {
            throw new Exception($"The managed x-ui inbound carries an invalid port: (server={server.Name}, port={inbound.Port})");
        }

        var stream = ParseStream(server, inbound);

        var address = string.IsNullOrWhiteSpace(server.DomainName) ? server.IpAddress : server.DomainName;

        return $"vless://{uuid}@{address}:{inbound.Port}?{BuildQuery(stream)}#{Uri.EscapeDataString(label)}";
    }

    private static XuiStreamSettings ParseStream(ServerEntity server, XuiInboundModel inbound)
    {
        if (string.IsNullOrWhiteSpace(inbound.StreamSettings))
        {
            return new XuiStreamSettings();
        }

        XuiStreamSettings? stream;

        try
        {
            stream = JsonSerializer.Deserialize<XuiStreamSettings>(inbound.StreamSettings, StreamJson);
        }
        catch (JsonException error)
        {
            throw new Exception(
                $"The x-ui inbound stream settings could not be parsed: (server={server.Name}, inbound-id={inbound.Id})",
                error);
        }

        return stream ?? new XuiStreamSettings();
    }

    private static string BuildQuery(XuiStreamSettings stream)
    {
        var network = (stream.Network ?? "tcp").ToLowerInvariant();
        var security = (stream.Security ?? "none").ToLowerInvariant();

        if (!SupportedNetworks.Contains(network))
        {
            throw new Exception($"The x-ui transport network is not supported for links: (network={network})");
        }

        if (!SupportedSecurities.Contains(security))
        {
            throw new Exception($"The x-ui transport security is not supported for links: (security={security})");
        }

        var parameters = new List<string>
        {
            "encryption=none",
            $"type={network}",
            $"security={security}",
        };

        switch (security)
        {
            case "reality":
                AddParameter(parameters, "sni", stream.RealitySettings?.ServerName);
                AddParameter(parameters, "fp", stream.RealitySettings?.Fingerprint);
                AddParameter(parameters, "pbk", stream.RealitySettings?.PublicKey);
                AddParameter(parameters, "sid", stream.RealitySettings?.ShortId);
                break;
            case "tls":
                AddParameter(parameters, "sni", stream.TlsSettings?.ServerName);
                AddParameter(parameters, "fp", stream.TlsSettings?.Fingerprint);
                break;
        }

        switch (network)
        {
            case "ws":
                AddParameter(parameters, "path", stream.WsSettings?.Path, escape: true);
                AddParameter(parameters, "host", FindHeader(stream.WsSettings?.Headers, "Host"));
                break;
            case "grpc":
                AddParameter(parameters, "serviceName", stream.GrpcSettings?.ServiceName, escape: true);
                break;
        }

        return string.Join("&", parameters);
    }

    private static void AddParameter(List<string> parameters, string key, string? value, bool escape = false)
    {
        if (string.IsNullOrEmpty(value)) return;

        parameters.Add($"{key}={(escape ? Uri.EscapeDataString(value) : value)}");
    }

    private static string? FindHeader(Dictionary<string, string>? headers, string name)
    {
        if (headers == null) return null;

        return headers.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private sealed class XuiStreamSettings
    {
        public string? Network { get; set; }

        public string? Security { get; set; }

        public XuiTlsSettings? TlsSettings { get; set; }

        public XuiRealitySettings? RealitySettings { get; set; }

        public XuiWsSettings? WsSettings { get; set; }

        public XuiGrpcSettings? GrpcSettings { get; set; }
    }

    private sealed class XuiTlsSettings
    {
        public string? ServerName { get; set; }

        public string? Fingerprint { get; set; }
    }

    private sealed class XuiRealitySettings
    {
        public string? ServerName { get; set; }

        public string? Fingerprint { get; set; }

        public string? PublicKey { get; set; }

        public string? ShortId { get; set; }
    }

    private sealed class XuiWsSettings
    {
        public string? Path { get; set; }

        public Dictionary<string, string>? Headers { get; set; }
    }

    private sealed class XuiGrpcSettings
    {
        public string? ServiceName { get; set; }
    }
}
