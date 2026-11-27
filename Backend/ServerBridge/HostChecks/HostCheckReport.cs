namespace PhotonBypass.ServerBridge.HostChecks;

/// <summary>
/// Result of one host-capability check run (Docs/25 D11). It is both the answer of
/// <c>POST /api/server/host-checks</c> and the source of the <c>host-check</c>
/// log lines. It carries hosts, ports, durations and error kinds only — never a
/// key, password or server config.
/// </summary>
public class HostCheckReport
{
    /// <summary>G1, one entry per probed destination.</summary>
    public List<HostCheckTargetResult> Targets { get; init; } = [];

    /// <summary>G1 overall: null means no destination was probed (nothing configured yet).</summary>
    public bool? OutboundTcp { get; init; }

    /// <summary>G2: whether a listener can be bound on the loopback address.</summary>
    public bool LoopbackBind { get; init; }

    /// <summary>G2: the loopback port the probe listener was bound to; zero when the bind failed.</summary>
    public int LoopbackPort { get; init; }

    /// <summary>G3: for how long this worker process has been alive when the report was produced.</summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>The same text as the summary log line.</summary>
    public string Summary { get; init; } = string.Empty;
}

public class HostCheckTargetResult
{
    /// <summary>The probed destination as <c>host:port</c>.</summary>
    public string Target { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public int Milliseconds { get; init; }

    /// <summary>The socket error code, <c>Timeout</c>, or the exception type name; null when the probe passed.</summary>
    public string? Error { get; init; }
}
