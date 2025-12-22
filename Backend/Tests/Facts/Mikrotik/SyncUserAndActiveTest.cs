using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.Mikrotik;

public class SyncUserAndActiveTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage1 = "Mikrotik-Base";

    [Fact]
    public async Task SimpleTest()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(TestPackage1, this);

        var server = new ServerEntity
        {
            IpAddress = MikrotikInitializer.GetIp(TestPackage1),
            OsType = Domain.Servers.Types.OperatingSystem.Mikrotik,
            Config = new ServerConfiguration
            {
                WebApiConfig = new WebApiConfig
                {
                    HostName = MikrotikInitializer.GetIp(TestPackage1),
                    Username = "admin",
                    Password = "admin",
                    Port = 8728,
                }
            }
        };

        var account = new AccountEntity
        {
            Username = "User01",
            VpnPassword = "User01",
        };

        var renewal = new RenewalEntity
        {
            SimultaneousUser = 1,
            TrafficLimit = 30 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 10,
        };

        var service = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncUserManagerService>();
        await service.SyncUserAndActive(server, account, renewal);
    }
}
