using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.ServerBridge.Services;

class ApiHandler(IHttpClientFactory factory) : IApiHandler
{
    public T LoginTo<T>(ServerEntity server, string http_client_key, string? base_url = null)
    {
        var config = server.Config?.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        if (string.IsNullOrEmpty(config.HostName))
        {
            throw new Exception($"The base-url configuration is not set for server ({server.Id}:{server.Name})");
        }

        // TODO: implement loging

        var base_path_field = typeof(T).GetField("BasePath");
        if (base_path_field != null)
        {
            var base_path = base_path_field.GetValue(null)?.ToString();
            if (string.IsNullOrEmpty(base_path))
            {
                base_url = $"{base_url?.Trim('/')}/{base_base_pathurl.Trim('/')}";
            }
        }

        var http = factory.CreateClient(http_client_key);

        base_url = $"{config.HttpsUrl}/{base_url.Trim('/')}";
        http.BaseAddress = new Uri(base_url);

        var auth_array = Encoding.ASCII.GetBytes($"{config.Username}:{config.Password}");
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(auth_array));

        return RestService.For<T>(http);
    }
}
