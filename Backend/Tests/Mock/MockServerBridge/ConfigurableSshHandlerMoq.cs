using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Tools;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Mock.MockServerBridge;

/// <summary>
/// Scriptable <see cref="ISshHandler"/> mock giving a test full control over
/// command responses. Unlike <see cref="SshHandlerMoq"/> it is deliberately not
/// marked with <c>IUnitLevelService</c>, so the test initializer does not
/// auto-discover it: a test class opts in from its <c>AddClassTestServices</c>
/// override via <see cref="CreateInstance"/> — the last DI registration wins,
/// replacing the default <see cref="SshHandlerMoq"/> for that class only.
/// </summary>
internal class ConfigurableSshHandlerMoq : Mock<ISshHandler>
{
    public const string NotScriptedMessage = "command is not scripted in ConfigurableSshHandlerMoq";

    private readonly Mock<ISshConnection> _connection = new();
    private readonly List<Rule> _rules = [];

    public ConfigurableSshHandlerMoq()
    {
        this.Setup(handler => handler.ConnectTo(It.IsAny<ServerEntity>()))
            .Returns((ServerEntity server) =>
            {
                lock (Connects) Connects.Add(server.Name);
                return Task.FromResult(_connection.Object);
            });

        _connection.Setup(connection => connection.Execute(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string command, out string result) =>
            {
                lock (Commands) Commands.Add(command);

                var rule = FindRule(command);

                if (rule == null)
                {
                    result = $"{NotScriptedMessage}: {command}";
                    return false;
                }

                result = rule.Response;
                return rule.Success;
            });
    }

    /// <summary>Executed SSH commands in execution order (thread-safe).</summary>
    public List<string> Commands { get; } = [];

    /// <summary>Names of the servers passed to <c>ConnectTo</c>, in call order (thread-safe).</summary>
    public List<string> Connects { get; } = [];

    public void RespondToPrefix(string command_prefix, string response) =>
        AddRule(command => command.StartsWith(command_prefix, StringComparison.Ordinal), response, success: true);

    public void RespondToPattern(string command_regex, string response) =>
        AddRule(new Regex(command_regex).IsMatch, response, success: true);

    public void FailToPrefix(string command_prefix, string error) =>
        AddRule(command => command.StartsWith(command_prefix, StringComparison.Ordinal), error, success: false);

    public void FailToPattern(string command_regex, string error) =>
        AddRule(new Regex(command_regex).IsMatch, error, success: false);

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<ConfigurableSshHandlerMoq>();
        services.AddLazyScoped<ISshHandler>(provider => provider.GetRequiredService<ConfigurableSshHandlerMoq>().Object);
    }

    private Rule? FindRule(string command)
    {
        lock (_rules) return _rules.FirstOrDefault(rule => rule.Match(command));
    }

    private void AddRule(Predicate<string> match, string response, bool success)
    {
        lock (_rules) _rules.Add(new Rule(match, response, success));
    }

    private sealed record Rule(Predicate<string> Match, string Response, bool Success);
}
