using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PhotonBypass.Domain.Servers.JsonType;

namespace PhotonBypass.Xui.WebService;

/// <summary>
/// The login contract of the x-ui panel (Docs/25 §4.5), shared by the adapter
/// and the connection test's service step so both exercise the same path.
/// </summary>
internal static class XuiLogin
{
    public const string Path = "login";

    /// <param name="port">
    /// Overrides <see cref="WebApiConfig.Port"/> in the URL: the connection
    /// test passes the loopback port of its own port-forward.
    /// </param>
    public static string BaseUrl(WebApiConfig config, int? port = null)
    {
        var scheme = config.Ssl ? "https" : "http";
        var port_part = (port ?? config.Port).HasValue ? $":{port ?? config.Port}" : "";
        var base_path = NormalizeBasePath(config.BasePath);

        return $"{scheme}://{config.HostName}{port_part}{base_path}";
    }

    public static string NormalizeBasePath(string? base_path)
    {
        var path = string.IsNullOrEmpty(base_path) ? "/" : base_path.Trim();

        if (!path.StartsWith('/')) path = "/" + path;
        if (!path.EndsWith('/')) path += "/";

        return path;
    }

    /// <summary>Returns the session cookie of the logged-in panel.</summary>
    public static async Task<string> LoginAsync(
        HttpClient http, string base_url, WebApiConfig config, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{base_url}{Path}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { username = config.Username, password = config.Password }),
                Encoding.UTF8, "application/json"),
        };

        using var response = await http.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<XuiResponseEnvelope>(cancellationToken: ct);

        if (envelope is not { Success: true })
        {
            // stays a plain Exception: the adapter's contract with its callers is the message, not a type
            throw new Exception(
                $"The x-ui login failed: (host={config.HostName}, message={envelope?.Msg ?? "unparseable response"})");
        }

        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            throw new XuiLoginUnexpectedException("The x-ui login response carried no session cookie.");
        }

        return string.Join("; ", cookies.Select(cookie => cookie.Split(';').First()));
    }

    private sealed class XuiResponseEnvelope
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }
    }
}

/// <summary>
/// The panel answered the login in an unexpected way — neither a transport
/// failure nor the documented credential rejection (a plain
/// <see cref="Exception"/> whose message starts with <see cref="RejectedMessagePrefix"/>).
/// </summary>
internal sealed class XuiLoginUnexpectedException(string message) : Exception(message);

internal static class XuiLoginFailure
{
    public const string RejectedMessagePrefix = "The x-ui login failed";

    /// <summary>The panel answered and refused the credentials, as opposed to a transport failure or an unexpected answer.</summary>
    public static bool IsRejection(Exception error) =>
        error.GetType() == typeof(Exception) && error.Message.StartsWith(RejectedMessagePrefix, StringComparison.Ordinal);
}

/// <summary>
/// Whether an x-ui request died on the wire — a broken connection, as
/// opposed to a non-success status, a timeout or a refused login.
/// </summary>
internal static class XuiTransportDeath
{
    public static bool Is(Exception error)
    {
        for (var current = error; current != null; current = current.InnerException)
        {
            if (current is IOException or SocketException) return true;
        }

        return false;
    }
}
