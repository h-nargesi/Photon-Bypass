using System.Diagnostics;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using Serilog;

namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// The SSH.NET connector behind both transports. It owns the host-key
/// decision (Docs/25 D4): the key is refused before authentication whenever
/// the policy does not accept it, and every failure leaves as a classified
/// <see cref="SshConnectException"/>.
/// </summary>
internal sealed class SshConnector : ISshConnector
{
    private enum HostKeyVerdict
    {
        Undecided,

        Trusted,

        Unpinned,

        Discovered,

        Missing,

        Mismatch,
    }

    public async Task<ISshChannel> ConnectAsync(SshEndpoint endpoint, CancellationToken ct)
    {
        var authentication = CreateAuthentication(endpoint);

        var client = new SshClient(new ConnectionInfo(endpoint.Host, endpoint.Port, endpoint.Username, authentication)
        {
            Timeout = endpoint.ConnectTimeout,
        })
        {
            KeepAliveInterval = endpoint.KeepAlive,
        };

        var verdict = HostKeyVerdict.Undecided;
        string? presented = null;
        string? key_name = null;

        client.HostKeyReceived += (_, key) =>
        {
            presented = "SHA256:" + key.FingerPrintSHA256;
            key_name = key.HostKeyName;

            verdict = endpoint.HostKeyPolicy switch
            {
                HostKeyPolicy.Discover => HostKeyVerdict.Discovered,
                HostKeyPolicy.Required when endpoint.HostKeySha256 == null => HostKeyVerdict.Missing,
                _ when endpoint.HostKeySha256 != null &&
                         !string.Equals(endpoint.HostKeySha256, presented, StringComparison.Ordinal) => HostKeyVerdict.Mismatch,
                _ => endpoint.HostKeySha256 == null ? HostKeyVerdict.Unpinned : HostKeyVerdict.Trusted,
            };

            key.CanTrust = verdict is HostKeyVerdict.Trusted or HostKeyVerdict.Unpinned;
        };

        var watch = Stopwatch.StartNew();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);

        limit.CancelAfter(endpoint.ConnectTimeout);

        try
        {
            await client.ConnectAsync(limit.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            client.Dispose();

            throw;
        }
        catch (Exception error)
        {
            client.Dispose();

            if (verdict == HostKeyVerdict.Discovered && presented != null)
            {
                // the connection test learns the fingerprint and stops here, before authentication
                return SshNetChannel.Discovered(new HostKeyCheck(presented, key_name, HostKeyStatus.Discovered));
            }

            throw new SshConnectException(
                Classify(error, verdict, limit.IsCancellationRequested),
                endpoint,
                presented,
                Step(presented),
                (int)watch.Elapsed.TotalMilliseconds,
                error);
        }

        if (verdict != HostKeyVerdict.Trusted && verdict != HostKeyVerdict.Unpinned || presented == null)
        {
            // refused keys and a session without a key exchange never leave this method as a usable channel
            client.Dispose();

            if (verdict == HostKeyVerdict.Discovered)
            {
                return SshNetChannel.Discovered(new HostKeyCheck(presented!, key_name, HostKeyStatus.Discovered));
            }

            throw new SshConnectException(
                Classify(null, verdict, false),
                endpoint,
                presented,
                Step(presented),
                (int)watch.Elapsed.TotalMilliseconds);
        }

        var status = verdict == HostKeyVerdict.Unpinned ? HostKeyStatus.Unpinned : HostKeyStatus.Trusted;

        return new SshNetChannel(client, endpoint, new HostKeyCheck(presented, key_name, status));
    }

    private static AuthenticationMethod CreateAuthentication(SshEndpoint endpoint)
    {
        if (endpoint.PrivateKey == null)
        {
            return new PasswordAuthenticationMethod(endpoint.Username, endpoint.Password ?? string.Empty);
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(endpoint.PrivateKey));

            return new PrivateKeyAuthenticationMethod(endpoint.Username, new PrivateKeyFile(stream, endpoint.PassPhrase!));
        }
        catch (Exception error)
        {
            // an unparsable key or a wrong passphrase fails before any network I/O
            throw new SshConnectException(SshConnectFailure.KeyInvalid, endpoint, step: "key-load", inner_exception: error);
        }
    }

    private static SshConnectFailure Classify(Exception? error, HostKeyVerdict verdict, bool timed_out)
    {
        return verdict switch
        {
            HostKeyVerdict.Missing => SshConnectFailure.HostKeyMissing,
            HostKeyVerdict.Mismatch => SshConnectFailure.HostKeyMismatch,
            _ when timed_out => SshConnectFailure.Timeout,
            _ => error switch
            {
                SshAuthenticationException => SshConnectFailure.Authentication,
                SshOperationTimeoutException => SshConnectFailure.Timeout,
                _ => SshConnectFailure.Connect,
            },
        };
    }

    private static string Step(string? presented) => presented == null ? "key-exchange" : "authentication";
}
