namespace PhotonBypass.ServerBridge.Email;

/// <summary>
/// Sliding-window ceiling over <see cref="EmailOptions.MaxSendsPerHour"/>:
/// reserves a send slot before any network I/O so an exhausted budget turns
/// into a fast typed failure instead of another SMTP connection. Attempts
/// count, not only successful deliveries — a feedback loop cannot budget its
/// way past the cap by failing. Registered singleton: every scoped
/// <see cref="EmailHandler"/> must see the same one-hour window.
/// </summary>
internal sealed class EmailSendLimiter(int cap, TimeProvider? time_provider = null)
{
    private readonly TimeProvider time_provider = time_provider ?? TimeProvider.System;

    private readonly object gate = new();

    private readonly Queue<DateTimeOffset> reservations = new();

    private static readonly TimeSpan WindowLength = TimeSpan.FromHours(1);

    public int Cap { get; } = cap;

    /// <summary>
    /// Reserves one send slot in the current window. Returns false when the
    /// cap is exhausted; always true for an unlimited (zero/negative) cap.
    /// </summary>
    public bool TryReserve()
    {
        if (Cap <= 0) return true;

        lock (gate)
        {
            var now = time_provider.GetUtcNow();

            while (reservations.Count > 0 && reservations.Peek() <= now - WindowLength)
            {
                reservations.Dequeue();
            }

            if (reservations.Count >= Cap)
            {
                return false;
            }

            reservations.Enqueue(now);

            return true;
        }
    }
}
