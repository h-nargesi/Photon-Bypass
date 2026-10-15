using FluentAssertions;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Ssh;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Tools;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

public class ProcessServiceTest : UnitLevelServiceInitializer
{
    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        ConfigurableSshHandlerMoq.CreateInstance(builder.Services);
        builder.Services.AddLazyScoped<IProcessService, ProcessService>();
    }

    [Fact]
    public async Task ActivateDeactivateCheck_OnOsMismatch_ThrowBeforeAnyConnection()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        var process = NewProcess(OperatingSystem.Ubuntu);
        var server = NewServer(OperatingSystem.Mikrotik);
        var context = new ProcessContext();

        (await process_service.Invoking(s => s.ActivateOn(process, server, context))
                .Should().ThrowAsync<Exception>())
            .WithMessage("Invalid OS process (Ubuntu) for server: test-server");

        (await process_service.Invoking(s => s.DeactivateOn(process, server, context))
                .Should().ThrowAsync<Exception>())
            .WithMessage("Invalid OS process (Ubuntu) for server: test-server");

        (await process_service.Invoking(s => s.CheckOn(process, server, context))
                .Should().ThrowAsync<Exception>())
            .WithMessage("Invalid OS process (Ubuntu) for server: test-server");

        ssh.Connects.Should().BeEmpty();
        ssh.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task MissingScriptBodies_ReturnFalseWithoutExecutingAnything()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        var process = NewProcess(OperatingSystem.Mikrotik, enable: null, disable: null, check: null);
        var server = NewServer(OperatingSystem.Mikrotik);

        (await process_service.ActivateOn(process, server, new ProcessContext())).Should().BeFalse();
        (await process_service.DeactivateOn(process, server, new ProcessContext())).Should().BeFalse();
        (await process_service.CheckOn(process, server, new ProcessContext())).Should().BeFalse();

        ssh.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckOn_WithoutCheckScript_ReturnsFalseBeforeAnyConnection()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        var process = NewProcess(OperatingSystem.Mikrotik, enable: null, disable: null, check: null);

        (await process_service.CheckOn(process, NewServer(), new ProcessContext())).Should().BeFalse();

        // CheckOn guards the null Check script before opening the connection
        ssh.Connects.Should().BeEmpty();
    }

    [Fact]
    public async Task Activate_InjectsContextVariablesIntoCommand()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.RespondToPrefix("/ip firewall", "done");

        var process = NewProcess(enable: [NewScript("/ip firewall address-list add list=block address={ip} comment={user}")]);
        var context = new ProcessContext { ["ip"] = "10.1.2.3", ["user"] = "ryan" };

        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeTrue();

        ssh.Commands.Should().ContainSingle()
            .Which.Should().Be("/ip firewall address-list add list=block address=10.1.2.3 comment=ryan");
    }

    [Fact]
    public async Task Activate_WithEmptyContext_SendsCommandUntouched()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.RespondToPrefix("/ip firewall", "done");

        var command = "/ip firewall address-list add list=block address={ip}";
        var process = NewProcess(enable: [NewScript(command)]);

        (await process_service.ActivateOn(process, NewServer(), new ProcessContext())).Should().BeTrue();

        ssh.Commands.Should().ContainSingle().Which.Should().Be(command);
    }

    [Theory]
    [InlineData("10.0.0.1;/system reboot")]
    [InlineData("10.0.0.1\n/system reboot")]
    [InlineData("10.0.0.1`reboot")]
    [InlineData("۱۰")]
    public async Task Activate_WithInvalidVariableValue_ThrowsAndExecutesNothing(string value)
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        var process = NewProcess(enable: [NewScript("/ip firewall address-list add address={ip}")]);
        var context = new ProcessContext { ["ip"] = value };

        (await process_service.Invoking(s => s.ActivateOn(process, NewServer(), context))
                .Should().ThrowAsync<Exception>())
            .Where(e => e.Message.StartsWith("Invalid character for inject: ip=", StringComparison.Ordinal));

        // the connection is opened before the first script runs, but no command may be executed
        ssh.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task Activate_WhenScriptFails_StopsChainAndSetsContextError()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.FailToPrefix("cmd-first", "script failed on NAS");

        var process = NewProcess(enable: [NewScript("cmd-first"), NewScript("cmd-second")]);
        var context = new ProcessContext();

        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeFalse();

        context.Error.Should().Be("script failed on NAS");
        ssh.Commands.Should().ContainSingle().Which.Should().Be("cmd-first");
    }

    [Fact]
    public async Task Activate_WhenCommandIsNotScripted_FailsWithDiagnosticError()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();

        var process = NewProcess(enable: [NewScript("cmd-unknown")]);
        var context = new ProcessContext();

        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeFalse();

        context.Error.Should().StartWith(ConfigurableSshHandlerMoq.NotScriptedMessage);
    }

    [Fact]
    public async Task Activate_WithReturnPattern_CollectsRawOutputAsResult()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.RespondToPrefix("cmd-a", "  raw output one ");
        ssh.RespondToPrefix("cmd-b", "  raw output two ");

        var process = NewProcess(enable:
        [
            NewScript("cmd-a", "return"),
            NewScript("cmd-b", "return"),
        ]);
        var context = new ProcessContext();

        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeTrue();

        context.Content.Should().Equal("  raw output one ", "  raw output two ");
        context.Result.Should().Be("  raw output one \n  raw output two ");
    }

    [Fact]
    public async Task Activate_WithoutOutputPattern_IgnoresOutput()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.RespondToPrefix("cmd-a", "ignored stdout");

        var process = NewProcess(enable: [NewScript("cmd-a")]);
        var context = new ProcessContext();

        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeTrue();

        context.Content.Should().BeEmpty();
        context.Result.Should().BeEmpty();
        context.Error.Should().BeNull();
    }

    [Fact]
    public async Task Activate_WithRegexPattern_WritesNamedGroupsToContext()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.RespondToPattern(@"^print stats$", "rx=100 tx=200");

        var pattern = "# regex:rx=(\\d+) tx=(\\d+)#1:rx#2:tx";
        var process = NewProcess(enable: [NewScript("print stats", pattern)]);

        var context = new ProcessContext();
        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeTrue();
        context["rx"].Should().Be("100");
        context["tx"].Should().Be("200");

        // same pattern again exercises the static pattern cache (second GetOrAdd hit)
        var second_context = new ProcessContext();
        var second_process = NewProcess(enable: [NewScript("print stats again", pattern)]);
        ssh.RespondToPattern(@"^print stats again$", "rx=300 tx=400");
        (await process_service.ActivateOn(second_process, NewServer(), second_context)).Should().BeTrue();
        second_context["rx"].Should().Be("300");
        second_context["tx"].Should().Be("400");
    }

    [Fact]
    public async Task Activate_WithMultipleRegexPatterns_ParsesEachSegment()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.RespondToPattern(@"^print stats$", "uptime=42 bytes=1024");

        var pattern = "# regex:uptime=(\\d+)#1:up# regex:bytes=(\\d+)#1:bytes";
        var process = NewProcess(enable: [NewScript("print stats", pattern)]);
        var context = new ProcessContext();

        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeTrue();

        context["up"].Should().Be("42");
        context["bytes"].Should().Be("1024");
    }

    [Fact]
    public async Task Activate_ChainsParsedOutputIntoNextScriptVariable()
    {
        using var scope = App.Services.CreateScope();
        var process_service = scope.ServiceProvider.GetRequiredService<IProcessService>();
        var ssh = scope.ServiceProvider.GetRequiredService<ConfigurableSshHandlerMoq>();
        ssh.RespondToPrefix("stats read", "address=10.0.0.9");
        ssh.RespondToPrefix("block ", "ok");

        var process = NewProcess(enable:
        [
            NewScript("stats read", "# regex:address=([\\d.]+)#1:address"),
            NewScript("block {address}"),
        ]);
        var context = new ProcessContext();

        (await process_service.ActivateOn(process, NewServer(), context)).Should().BeTrue();

        ssh.Commands.Should().Equal("stats read", "block 10.0.0.9");
    }

    private static ServerEntity NewServer(OperatingSystem os = OperatingSystem.Mikrotik) => new()
    {
        Name = "test-server",
        IpAddress = "10.10.10.10",
        DomainName = "test-server.local",
        RealmId = 1,
        OsType = os,
    };

    private static ProcessEntity NewProcess(
        OperatingSystem? os = null,
        List<ScriptEntity>? enable = null,
        List<ScriptEntity>? disable = null,
        ScriptEntity? check = null) => new()
    {
        Name = "test-process",
        OsType = os ?? OperatingSystem.Mikrotik,
        Enable = enable,
        Disable = disable,
        Check = check,
    };

    private static ScriptEntity NewScript(string content, string? output_pattern = null) => new()
    {
        Name = "script",
        Content = content,
        OutputPattern = output_pattern,
    };
}
