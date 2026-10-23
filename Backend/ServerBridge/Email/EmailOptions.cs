namespace PhotonBypass.ServerBridge.Email;

public class EmailOptions
{
    /// <summary>
    /// Master switch. False (the default) turns every send into a logged
    /// no-op so a Development environment without SMTP never throws —
    /// production must set it to true (ProductionSecretsGuard enforces it).
    /// </summary>
    public bool Enabled { get; set; }

    public string? FullName { get; set; }

    public string? Address { get; set; }

    public string? Password { get; set; }

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public int SendTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Process-wide ceiling of email sends per sliding hour. Zero or negative
    /// means unlimited. A run-away safety valve against feedback loops (e.g. a
    /// social-channel outage falling back to email en masse): attempts are
    /// counted, not only successful deliveries.
    /// </summary>
    public int MaxSendsPerHour { get; set; } = 500;
}
