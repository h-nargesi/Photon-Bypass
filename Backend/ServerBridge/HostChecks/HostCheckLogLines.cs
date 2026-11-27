using System.Globalization;

namespace PhotonBypass.ServerBridge.HostChecks;

/// <summary>
/// The <c>host-check</c> log lines of Docs/25 §4.11. The deploy runbook greps
/// for these exact shapes, so they are built in one place.
/// </summary>
internal static class HostCheckLogLines
{
    private const string Prefix = "host-check";

    public static string Target(HostCheckTargetResult result)
    {
        return $"{Prefix} G1 tcp target={result.Target} pass={Flag(result.Passed)} ms={result.Milliseconds} error={result.Error ?? "none"}";
    }

    public static string LoopbackBind(bool passed, int port, string? error)
    {
        return $"{Prefix} G2 loopback-bind pass={Flag(passed)} port={port} error={error ?? "none"}";
    }

    public static string WorkerStarted(int pid, string machine, string os)
    {
        return $"{Prefix} G3 worker-started pid={pid} machine={machine} os={os}";
    }

    public static string Heartbeat(TimeSpan uptime)
    {
        return $"{Prefix} G3 heartbeat uptime={FormatUptime(uptime)}";
    }

    public static string Summary(bool? outbound_tcp, bool loopback_bind, int targets)
    {
        var g1 = Verdict(outbound_tcp);
        var g2 = loopback_bind ? "pass" : "fail";

        return $"{Prefix} summary g1={g1} g2={g2} g3=observing targets={targets}";
    }

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Verdict(bool? passed) => passed switch { null => "no-targets", true => "pass", false => "fail" };

    private static string FormatUptime(TimeSpan uptime) => uptime.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture);
}
