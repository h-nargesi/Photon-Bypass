using PhotonBypass.ServerBridge.Api;

namespace PhotonBypass.FreeRadius.WebService;

public class RadiusDeskApiCall(IHttpClientFactory factory) : ApiCall(factory)
{
    public const string HttpClientKeyName = "free-radius";
    
    protected override string? BaseUrl => "cake4/rd_cake";

    protected override string HttpClientKey => HttpClientKeyName;
}