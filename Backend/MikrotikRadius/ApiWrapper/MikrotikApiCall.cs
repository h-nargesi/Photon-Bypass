using PhotonBypass.ServerBridge.Api;

namespace PhotonBypass.Mikrotik.Radius.ApiWrapper;

public class MikrotikApiCall(IHttpClientFactory factory) : ApiCall(factory)
{
    public const string HttpClientKeyName = "mikrotik-api";
    
    protected override string? BaseUrl => "rest";

    protected override string HttpClientKey => HttpClientKeyName;
}