namespace PhotonBypass.ServerBridge.Hosted;

/// <summary>
/// The stop signal of a hosted service's background work. A host may be
/// stopped twice — once by its run loop and once by whoever owns it — and
/// disposed before the second stop: stopping is idempotent and never throws
/// once the token is gone.
/// </summary>
internal sealed class StoppingToken : IDisposable
{
    private readonly CancellationTokenSource source = new();

    private int stopped;

    private int disposed;

    /// <summary>The token the background work waits on.</summary>
    public CancellationToken Token => source.Token;

    /// <summary>Whether the background work was told to end, or cannot be told anymore.</summary>
    public bool IsStopped
    {
        get
        {
            try
            {
                return Volatile.Read(ref stopped) != 0 || source.IsCancellationRequested;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }
    }

    /// <summary>Ends the background work; false when it had already ended.</summary>
    public bool TryStop()
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0) return false;

        Cancel();

        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        Cancel();

        source.Dispose();
    }

    private void Cancel()
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // disposed without a stop: nothing is left to signal
        }
    }
}
