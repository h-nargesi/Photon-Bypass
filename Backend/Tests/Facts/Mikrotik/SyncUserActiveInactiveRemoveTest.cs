using FluentAssertions;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Mikrotik.Radius.Application;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using tik4net.Objects;

namespace PhotonBypass.Test.Facts.Mikrotik;

public class SyncUserActiveInactiveRemoveTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage1 = "Mikrotik-Base";

    private static readonly ServerEntity Server = new()
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

    [Fact]
    public async Task SimpleTest_SameStep_1()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(TestPackage1, this);

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

        var service = scope.ServiceProvider
            .GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.UserManager);
        var handler = scope.ServiceProvider.GetRequiredService<ITik4NetHandler>();
        await service.SyncUserAndActive(Server, account, renewal);

        using var connection = await handler.ConnectTo(Server);

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

        result = connection.GetProfile(renewal.TimeLimitInDays, renewal.TrafficLimit, renewal.RateLimitInMeg,
            out var profile);
        Assert.True(result);
        Assert.NotNull(profile);

        connection.GetLimitationAssignment(profile.Name, limitation_names, out var adding_list, out var removing_list);
        Assert.Empty(adding_list);
        Assert.Empty(removing_list);

        // check user
        var user = connection.GetUser(account.Username);
        Assert.NotNull(user);

        // assign user to profile
        var assign = connection.LoadList<UserProfileModel>(
            TikParam.Equal<UserProfileModel>(nameof(UserProfileModel.Username), user.Name),
            TikParam.Equal<UserProfileModel>(nameof(UserProfileModel.Profile), profile.Name));
        Assert.NotNull(assign);

        var data = connection.LoadList<UserProfileModel>(
            TikParam.Greater<UserProfileModel>(nameof(UserProfileModel.EndTime), DateTime.Now.ToString("s"))).ToList();
        Assert.NotNull(data);

        // change password
        Assert.Equal(account.VpnPassword, user.Password);
        await service.ChangeVpnPassword(Server, account.Username, "new-password");
        user = connection.GetUser(account.Username);
        Assert.NotNull(user);
        Assert.Equal(account.Username, user.Name);
        Assert.Equal("new-password", user.Password);
    }

    [Fact]
    public async Task SimpleTest_SameStep_2()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(TestPackage1, this);

        var account = new AccountEntity
        {
            Username = "User01",
            VpnPassword = "User01",
        };

        var second = new AccountEntity
        {
            Username = "User02",
            VpnPassword = "User02",
        };

        var renewal = new RenewalEntity
        {
            SimultaneousUser = 1,
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 10,
        };

        var service = scope.ServiceProvider
            .GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.UserManager);
        var handler = scope.ServiceProvider.GetRequiredService<ITik4NetHandler>();
        await service.SyncUserAndActive(Server, account, renewal);
        await service.SyncUserAndActive(Server, second, renewal);

        using var connection = await handler.ConnectTo(Server);

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

        result = connection.GetProfile(renewal.TimeLimitInDays, renewal.TrafficLimit, renewal.RateLimitInMeg,
            out var profile);
        Assert.True(result);
        Assert.NotNull(profile);

        connection.GetLimitationAssignment(profile.Name, limitation_names, out var adding_list, out var removing_list);
        Assert.Empty(adding_list);
        Assert.Empty(removing_list);

        // check user
        var user = connection.GetUser(second.Username);
        Assert.NotNull(user);
        Assert.Equal(second.Username, user.Name);
        Assert.False(user.Disabled);

        user = connection.GetUser(account.Username);
        Assert.NotNull(user);
        Assert.Equal(account.Username, user.Name);
        Assert.False(user.Disabled);

        // assign user to profile
        var assign = connection.LoadList<UserProfileModel>(
            TikParam.Equal<UserProfileModel>(nameof(UserProfileModel.Username), user.Name),
            TikParam.Equal<UserProfileModel>(nameof(UserProfileModel.Profile), profile.Name));
        Assert.NotNull(assign);

        var data = connection.LoadList<UserProfileModel>(
            TikParam.Greater<UserProfileModel>(nameof(UserProfileModel.EndTime), DateTime.Now.ToString("s"))).ToList();
        Assert.NotNull(data);

        // inactive
        await service.DeactivateUser(Server, [account.Username, second.Username]);
        
        user = connection.GetUser(second.Username);
        Assert.NotNull(user);
        Assert.Equal(second.Username, user.Name);
        Assert.True(user.Disabled);

        user = connection.GetUser(account.Username);
        Assert.NotNull(user);
        Assert.Equal(account.Username, user.Name);
        Assert.True(user.Disabled);

        // inactive exception
        await service.SyncUserAndActive(Server, account, renewal);
        await service.SyncUserAndActive(Server, second, renewal);
        
        user = connection.GetUser(second.Username);
        Assert.NotNull(user);
        Assert.Equal(second.Username, user.Name);
        Assert.False(user.Disabled);

        user = connection.GetUser(account.Username);
        Assert.NotNull(user);
        Assert.Equal(account.Username, user.Name);
        Assert.False(user.Disabled);
        
        await service.DeactivateUserExcept(Server, [account.Username]);
        
        user = connection.GetUser(second.Username);
        Assert.NotNull(user);
        Assert.Equal(second.Username, user.Name);
        Assert.True(user.Disabled);

        user = connection.GetUser(account.Username);
        Assert.NotNull(user);
        Assert.Equal(account.Username, user.Name);
        Assert.False(user.Disabled);

        // remove
        await service.RemoveUsers(Server, [account.Username]);
        
        user = connection.GetUser(second.Username);
        Assert.NotNull(user);
        Assert.Equal(second.Username, user.Name);
        Assert.True(user.Disabled);

        user = connection.GetUser(account.Username);
        Assert.Null(user);
    }

    [Fact]
    public async Task LimitationError()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(TestPackage1, this);

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

        var service =
            scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.UserManager);
        var func = () => service.SyncUserAndActive(Server, account, renewal);

        await func.Should().ThrowAsync<Exception>("Traffic limit must be a multiple of 25 gigabytes.");
    }
}