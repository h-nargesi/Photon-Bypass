namespace PhotonBypass.Portal.Context;

public class HistoryContext
{
    public long? From { get; set; }

    public long? To { get; set; }

    public DateTime? FromDateTime => From != null
        ? DateTimeOffset.FromUnixTimeMilliseconds(From.Value).LocalDateTime
        : null;

    public DateTime? ToDateTime => To != null
        ? DateTimeOffset.FromUnixTimeMilliseconds(To.Value).LocalDateTime
        : null;
}
