using System.Diagnostics;
using System.Text;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.ServerBridge.Tunnel;

internal enum SshConnectFailure
{
    HostKeyMismatch,

    HostKeyMissing,

    Authentication,

    KeyInvalid,

    Timeout,

    Connect,
}

/// <summary>
/// A failed SSH connect attempt, classified before it reaches a consumer:
/// consumers turn it into a <see cref="UserException"/> through
/// <see cref="SshErrors.ToUserException"/>. Its message never carries key,
/// passphrase or password material — only host, port, username and host-key
/// fingerprints.
/// </summary>
internal sealed class SshConnectException : Exception
{
    public SshConnectException(
        SshConnectFailure failure, SshEndpoint endpoint, string? presented_host_key = null,
        string? step = null, int elapsed_milliseconds = 0, Exception? inner_exception = null)
        : base(
            $"SSH connect to {endpoint.Host}:{endpoint.Port} failed ({failure}, step={step ?? "connect"}, {elapsed_milliseconds} ms)",
            inner_exception)
    {
        Failure = failure;
        Endpoint = endpoint;
        PresentedHostKey = presented_host_key;
        Step = step;
        ElapsedMilliseconds = elapsed_milliseconds;
    }

    public SshConnectFailure Failure { get; }

    public SshEndpoint Endpoint { get; }

    public string? PresentedHostKey { get; }

    public string? Step { get; }

    public int ElapsedMilliseconds { get; }
}

/// <summary>
/// The user-facing wording of every SSH connect failure (Docs/25 §4.2).
/// </summary>
internal static class SshErrors
{
    public const string HostKeyMismatchMessage = "کلید میزبان سرور با مقدار ثبت‌شده مطابقت ندارد!";

    public const string HostKeyMissingMessage = "برای این سرور کلید میزبان (host key) ثبت نشده است!";

    public const string AuthenticationMessage = "احراز هویت SSH ناموفق بود!";

    public const string ConnectMessage = "خطای اتصال به سرور!";

    public const string CommandTimeoutMessage = "خطای اجرای دستور در سرور!";

    public const string KeyInvalidMessage = "کلید خصوصی SSH معتبر نیست!";

    public static UserException ToUserException(SshConnectException error)
    {
        var endpoint = error.Endpoint;

        return error.Failure switch
        {
            SshConnectFailure.HostKeyMismatch => new UserException(HostKeyMismatchMessage,
                $"Host key mismatch on {endpoint.Host}:{endpoint.Port}: presented {error.PresentedHostKey}, pinned {endpoint.HostKeySha256}"),

            SshConnectFailure.HostKeyMissing => new UserException(HostKeyMissingMessage,
                "HostKeySha256 is empty; run the connection test to discover it"),

            SshConnectFailure.Authentication => new UserException(AuthenticationMessage,
                $"SSH authentication failed on {endpoint.Host}:{endpoint.Port} as {endpoint.Username}"),

            SshConnectFailure.KeyInvalid => new UserException(KeyInvalidMessage,
                $"The private key could not be loaded for {endpoint.Host}:{endpoint.Port} ({error.InnerException?.GetType().Name ?? "invalid key"})"),

            _ => new UserException(ConnectMessage,
                $"Cannot connect to {endpoint.Host}:{endpoint.Port} ({error.Step ?? "connect"}) after {error.ElapsedMilliseconds} ms"),
        };
    }
}

/// <summary>Raw result of one remote command: exit status (-1 when none arrived), stdout and stderr.</summary>
internal readonly record struct SshCommandResult(int ExitStatus, string Output, string Error);

internal interface ISshConnector
{
    Task<ISshChannel> ConnectAsync(SshEndpoint endpoint, CancellationToken ct);
}

internal interface ISshChannel : IDisposable
{
    bool IsConnected { get; }

    /// <summary>The host key presented during key exchange; only meaningful once a key was received.</summary>
    HostKeyCheck HostKey { get; }

    /// <summary>Session-level failure (connection lost, keep-alive failure).</summary>
    event Action<Exception>? ErrorOccurred;

    /// <summary>Starts (or reuses) a loopback listener forwarding to the server's own <c>127.0.0.1:remotePort</c>; returns the bound port.</summary>
    int StartLocalForward(int remotePort);

    Task<ISshCommandChannel> OpenCommandChannelAsync(CancellationToken ct);

    void Disconnect();
}

internal interface ISshCommandChannel : IDisposable
{
    string Host { get; }

    /// <summary>Runs one command; throws <see cref="TimeoutException"/> on timeout and <see cref="SshConnectException"/> when the session is gone.</summary>
    SshCommandResult Execute(string command, TimeSpan timeout);

    void Cancel();
}
