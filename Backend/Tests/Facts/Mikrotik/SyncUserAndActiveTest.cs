using FluentAssertions;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Mikrotik.Radius.Application;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using tik4net.Objects;

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
                    Ssl = false,
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
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 10,
        };

        var service = scope.ServiceProvider.GetRequiredService<IAccountRadiusSyncUserManagerService>();
        var handler = scope.ServiceProvider.GetRequiredService<ITik4NetHandler>();
        await service.SyncUserAndActive(server, account, renewal);

        using var connection = await handler.ConnectTo(server);

        bool result;
        var limitation_names = new HashSet<string>();

        // Check Speed
        if (renewal.RateLimitInMeg.HasValue)
        {
            result = connection.GetRateLimit(renewal.RateLimitInMeg.Value, out var limitation);
            Assert.True(result);
            Assert.NotNull(limitation);

            limitation_names.Add(limitation.Name);
        }

        // Check Traffic
        if (renewal.TrafficLimit.HasValue)
        {
            result = connection.GetTrafficLimit(renewal.TrafficLimit.Value, out var limitation);
            Assert.True(result);
            Assert.NotNull(limitation);

            limitation_names.Add(limitation.Name);
        }

        result = connection.GetProfile(renewal.TimeLimitInDays, renewal.TrafficLimit, renewal.RateLimitInMeg, out var profile);
        Assert.True(result);
        Assert.NotNull(profile);

        connection.GetLimitationAssignment(profile.Name, limitation_names, out var adding_list, out var removing_list);
        Assert.Empty(adding_list);
        Assert.Empty(removing_list);

        // check user
        result = connection.GetUser(account, renewal.SimultaneousUser, out var user);
        Assert.True(result);
        Assert.NotNull(user);

        // assign user to profile
        var assign = connection.LoadList<UserProfileModel>(
            TikParam.Equal<UserProfileModel>(nameof(UserProfileModel.Username), user.Name),
            TikParam.Equal<UserProfileModel>(nameof(UserProfileModel.Profile), profile.Name));
        Assert.NotNull(assign);
        Assert.Single(assign);
    }


    [Fact]
    public async Task LimitationError()
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
                    Ssl = false,
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
        var func = () => service.SyncUserAndActive(server, account, renewal);

        await func.Should().ThrowAsync<Exception>("Traffic limit must be a multiple of 25 gigabytes.");
    }
}
