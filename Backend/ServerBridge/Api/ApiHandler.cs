using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using Refit;
using System.Net.Http.Headers;
using System.Text;

namespace PhotonBypass.ServerBridge.Api;

class ApiHandler(IHttpClientFactory factory) : IApiHandler
{
    //private string? token;

    public string? HttpClientKey { get; set; }

    public T LoginTo<T>(ServerEntity server)
    {
        if (string.IsNullOrEmpty(HttpClientKey))
        {
            throw new Exception("The 'HttpClientKey' is not set.");
        }

        var config = server.Config?.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        if (string.IsNullOrEmpty(config.HostName))
        {
            throw new Exception($"The base-url configuration is not set for server ({server.Id}:{server.Name})");
        }

        // TODO: implement loging

        var http = factory.CreateClient(HttpClientKey);

        var base_url = http.BaseAddress?.ToString();
        var base_path_field = typeof(T).GetField("BasePath");
        if (base_path_field != null)
        {
            var base_path = base_path_field.GetValue(null)?.ToString();
            if (!string.IsNullOrEmpty(base_path))
            {
                base_url = $"{base_url?.Trim('/')}/{base_path.Trim('/')}";
            }
        }

        base_url = $"{config.HttpsUrl}/{base_url?.Trim('/')}";
        http.BaseAddress = new Uri(base_url.Trim('/'));

        var auth_array = Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}");
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(auth_array));

        return RestService.For<T>(http);
    }
}
