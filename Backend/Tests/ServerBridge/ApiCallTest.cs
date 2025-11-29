using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Mikrotik.Radius;
using PhotonBypass.Mikrotik.Radius.ApiWrapper;

namespace PhotonBypass.Test.ServerBridge;

public class ApiCallTest : ServiceInitializer
{
    [Fact]
    public async Task UserManagerSessionPrint()
    {
        using var scope = App.Services.CreateScope();
        var call = new MikrotikApiCall(scope.ServiceProvider.GetRequiredService<IHttpClientFactory>());
        var sessions = call.PrepareApi<ISessions>(new ServerEntity
        {
            Config = new ServerConfiguration
            {
                WebApiConfig = new WebApiConfig
                {
                    HostName = "gnb05.photon-bypass.com",
                    Username = "admin",
                    Password = "MzR8EfK4WacnkHGT3Ax72s",
                }
            }
        });
        
        var sessions_list = await sessions.PrintByUsername("hamed_test");
        
    }
}