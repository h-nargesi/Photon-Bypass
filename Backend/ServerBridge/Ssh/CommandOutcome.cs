using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge.Tunnel;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.ServerBridge.Ssh;

internal readonly record struct CommandVerdict(bool Success, string Result);

/// <summary>
/// Decides whether a remote command succeeded from its raw result — the same
/// way for both transports (Docs/25 D10): non-empty stderr, then a positive
/// exit status, then — on RouterOS — an error text on stdout. Anything else
/// is a success.
/// </summary>
internal static class CommandOutcome
{
    public static CommandVerdict Classify(SshCommandResult execution, OperatingSystem os)
    {
        if (!string.IsNullOrEmpty(execution.Error)) return new(false, execution.Error);

        if (execution.ExitStatus > 0)
        {
            return new(false, string.IsNullOrWhiteSpace(execution.Output) ? $"exit-status {execution.ExitStatus}" : execution.Output);
        }

        if (os == OperatingSystem.Mikrotik && RouterOsErrorPatterns.IsAny(execution.Output)) return new(false, execution.Output);

        return new(true, execution.Output);
    }
}
