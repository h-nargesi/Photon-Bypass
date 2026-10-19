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
}
