namespace PhotonBypass.API.Context;

public class CloseConnectionContext
{
    public string? Target { get; set; }

    public string? Server { get; set; }

    public string? SessionId { get; set; }
}
