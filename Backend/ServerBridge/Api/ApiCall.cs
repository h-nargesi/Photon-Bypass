using PhotonBypass.Domain.Servers.Entity;
using Refit;
using System.Net.Http.Headers;
using System.Text;

namespace PhotonBypass.ServerBridge.Api;

public abstract class ApiCall(IHttpClientFactory factory)
{
    protected abstract string? BaseUrl { get; }

    protected abstract string HttpClientKey { get; }

    public T PrepareApi<T>(ServerEntity server, string? base_path = null)
    {
        var config = server.Config?.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        if (string.IsNullOrEmpty(config.HostName))
        {
            throw new Exception($"The base-url configuration is not set for server ({server.Id}:{server.Name})");
        }

        var http = factory.CreateClient(HttpClientKey);

        base_path = $"{BaseUrl?.Trim('/')}/{base_path?.Trim('/')}";
        base_path = $"https://{config.HostName}/{base_path.Trim('/')}";
        http.BaseAddress = new Uri(base_path);

        var auth_array = Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}");
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(auth_array));

        return RestService.For<T>(http);
    }
}
