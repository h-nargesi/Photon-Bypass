using System.Net.Sockets;

namespace PhotonBypass.ServerBridge.HostChecks;

internal readonly record struct TcpProbeResult(bool Passed, int Milliseconds, string? Error);

/// <summary>
/// One outbound TCP connect with a bounded wait and an immediate close.
/// Shared by the host-capability check (Docs/25 G1) and the connection
/// test's <c>tcp</c> step.
/// </summary>
internal static class TcpProbe
{
    public static async Task<TcpProbeResult> ConnectAsync(
        string host, int port, TimeSpan timeout, TimeProvider time_provider, CancellationToken ct)
    {
        var started = time_provider.GetTimestamp();

        string? error = null;

        try
        {
            using var client = new TcpClient();
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);

            limit.CancelAfter(timeout);

            await client.ConnectAsync(host, port, limit.Token);
        }
        catch (SocketException failure)
        {
            error = failure.SocketErrorCode.ToString();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            error = "Timeout";
        }
        catch (Exception failure)
        {
            error = failure.GetType().Name;
        }

        return new TcpProbeResult(
            error == null,
            (int)time_provider.GetElapsedTime(started).TotalMilliseconds,
            error);
    }
}
