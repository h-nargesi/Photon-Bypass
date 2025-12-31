using FluentAssertions;
using PhotonBypass.Application.Connection;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Application;

public class ConnectionApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public Task GetCurrentConnectionState_Invalid()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.GetCurrentConnectionState("Invalid username");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task GetCurrentConnectionState_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.GetCurrentConnectionState("InactiveUser7");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task GetCurrentConnectionState_NoPlan()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.GetCurrentConnectionState("User6");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetCurrentConnectionState_RealmWithNoRadius()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var result_list = (await connection_app.GetCurrentConnectionState("User4")).Data;

        Assert.NotNull(result_list);
        Assert.Empty(result_list);
    }

    [Fact]
    public async Task GetCurrentConnectionState()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var result_list = (await connection_app.GetCurrentConnectionState("User3")).Data;

        Assert.NotNull(result_list);
        Assert.Equal(2, result_list.Count);
    }

    [Fact]
    public Task CloseConnection_InvalidAccount()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("ip", "Invalid user name", "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("ip", "InactiveUser7", "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_NoPlan()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("ip", "User6", "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InvalidIp()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("10.10.10.10", "User4", "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InvalidServer()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.11", "User4", "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_NoRadius()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.21", "User2", "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InvalidSessionId()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.11", "User99", "invalid session");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.11", "User99", "0F52A7");

        return func.Should().ThrowAsync<UserException>();
    }
}