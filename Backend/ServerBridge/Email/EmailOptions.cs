namespace PhotonBypass.ServerBridge.Email;

public class EmailOptions
{
    public string? FullName { get; set; }

    public string? Address { get; set; }

    public string? Password { get; set; }

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public int SendTimeoutSeconds { get; set; } = 30;
}
