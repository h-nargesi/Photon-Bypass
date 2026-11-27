using tik4net;

namespace PhotonBypass.ServerBridge.Tik4net;

/// <summary>
/// Opens a tik4net connection within a bounded wait. The timeout surfaces as
/// <see cref="TimeoutException"/>; the connection is left to the caller.
/// Shared by the User Manager adapter and the connection test's service step.
/// </summary>
public static class TikConnectionOpener
{
    public static async Task OpenAsync(
        ITikConnection connection, string host, int port, string username, string password,
        TimeSpan timeout, CancellationToken ct = default)
    {
        var open_task = connection.OpenAsync(host, port, username, password);

        try
        {
            await open_task.WaitAsync(timeout, ct);
        }
        catch (Exception error) when (error is TimeoutException ||
                                      (error is OperationCanceledException && ct.IsCancellationRequested))
        {
            // the abandoned open may still fail later: keep its exception observed
            _ = open_task.ContinueWith(
                task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            throw;
        }
    }
}
