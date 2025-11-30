using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Mikrotik.Radius.ApiWrapper;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net.Objects;
using tik4net.Objects.Ppp;

namespace PhotonBypass.Test.ServerBridge;

public class ApiCallTest : ServiceInitializer
{
    private readonly static ServerEntity server = new ServerEntity
    {
        Config = new ServerConfiguration
        {
            WebApiConfig = new WebApiConfig
            {
                HostName = "fnb05.photon-bypass.com",
                Port = 9119,
                Username = "admin",
                Password = "MzR8EfK4WacnkHGT3Ax72s",
            }
        }
    };

    [Fact(Skip = "Not Implemented")]
    public async Task UserManagerSessionPrintApi()
    {
        using var scope = App.Services.CreateScope();
        var call = new MikrotikApiCall(scope.ServiceProvider.GetRequiredService<IHttpClientFactory>());
        var sessions = call.PrepareApi<ISessions>(server);
        
        var sessions_list = await sessions.PrintByUsername("test_hamed@aw");        
    }

    [Fact]
    public async Task PppActivePrintTik()
    {
        using var connection = await server.TikApiConnect();

        var active_ppp_list = connection.LoadList<PppActive>(new TikParam("name", "Anahid@ry"));
    }

    [Fact]
    public async Task UserManaerSessionPrintTik()
    {
        using var connection = await server.TikApiConnect();

        var active_ppp_list = connection.LoadList<SessionModel>(new TikParam("user", "test_hamed@aw"));
    }
}
