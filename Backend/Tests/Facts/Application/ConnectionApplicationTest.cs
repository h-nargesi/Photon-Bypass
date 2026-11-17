using FluentAssertions;
using PhotonBypass.Application.Connection;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.Application;

public class ConnectionApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public Task GetCurrentConnectionState_Invalid()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.GetCurrentConnectionState(123_456);

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task GetCurrentConnectionState_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.GetCurrentConnectionState(7);

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetCurrentConnectionState_NoPlan()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();

        // Docs/08 contract kept: live-session listing requires an active plan
        var func = () => connection_app.GetCurrentConnectionState(6);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetCurrentConnectionState_RealmWithNoRadius()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var result_list = (await connection_app.GetCurrentConnectionState(4)).Data;

        Assert.NotNull(result_list);
        Assert.Empty(result_list);
    }

    [Fact]
    public async Task GetCurrentConnectionState()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var result_list = (await connection_app.GetCurrentConnectionState(3)).Data;

        Assert.NotNull(result_list);
        Assert.Equal(2, result_list.Count);
    }

    [Fact]
    public Task CloseConnection_InvalidAccount()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("ip", 123_456, "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("ip", 7, "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_NoPlan()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("ip", 6, "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InvalidIp()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("10.10.10.10", 4, "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InvalidServer()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.11", 4, "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_NoRadius()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.21", 2, "session_id");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public Task CloseConnection_InvalidSessionId()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.11", 99, "invalid session");

        return func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task CloseConnection_ForeignSession()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();

        var func = () => connection_app.CloseConnection("192.168.125.31", 3, "2036A2");

        (await func.Should().ThrowAsync<UserException>())
            .Where(ex => ex.UserMessage == "دسترسی غیرمجاز!");
    }

    [Fact]
    public async Task CloseConnection_OwnActiveSession()
    {
        using var scope = App.Services.CreateScope();

        var is_closed = false;
        var tik4_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();
        tik4_moq.OnExecute += (command_text, _) =>
        {
            if (command_text == "close-session")
            {
                is_closed = true;
            }
        };

        var result = await scope.ServiceProvider.GetRequiredService<IConnectionApplication>()
            .CloseConnection("192.168.125.31", 3, "2037B3");

        Assert.Equal(2, result.Code / 100);
        Assert.True(is_closed);
    }

    [Fact]
    public Task CloseConnection()
    {
        using var scope = App.Services.CreateScope();
        var connection_app = scope.ServiceProvider.GetRequiredService<IConnectionApplication>();
        var func = () => connection_app.CloseConnection("192.168.125.11", 99, "0F52A7");

        return func.Should().ThrowAsync<UserException>();
    }
}
