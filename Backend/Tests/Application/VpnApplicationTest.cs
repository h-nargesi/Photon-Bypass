using FluentAssertions;
using PhotonBypass.Application.Vpn;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.MockServerBridge;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class VpnApplicationTest : ServiceInitializer
{
    private static readonly DateTime Today = DateTime.Now.Date;

    [Fact]
    public async Task ChangeVpnPassword_InvlaidAccount()
    {
        using var scope = App.Services.CreateScope();
        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("Invalid Username", "new_password");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangeVpnPassword_DisabledAccount()
    {
        using var scope = App.Services.CreateScope();
        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User7", "new_password");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangeVpnPassword_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User6", "new_password");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangeVpnPassword()
    {
        const string new_password = "new-password";

        using var scope = App.Services.CreateScope();

        var is_saved = false;
        var tik4_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();
        tik4_moq.OnExecute += (command_text, parameters) =>
        {
            is_saved = true;
            Assert.Equal("/user-manager/user/set", command_text);
            Assert.Equal(2, parameters.Count);
            Assert.Equal("1", parameters.Where(p => p.Name == ".id").Select(p => p.Value).FirstOrDefault());
            Assert.Equal(new_password, parameters.Where(p => p.Name == "password").Select(p => p.Value).FirstOrDefault());
        };

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User1", new_password);

        Assert.True(is_saved);
    }

    [Fact]
    public async Task TrafficData_User1()
    {
        using var scope = App.Services.CreateScope();

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .TrafficData("User1");

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(Today.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }

    [Fact]
    public async Task TrafficData_User2()
    {
        using var scope = App.Services.CreateScope();

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .TrafficData("User2");

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(Today.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }
}