namespace PhotonBypass.Domain.Model.Connection;

public class CloseConnectionContext
{
    public ushort? Index { get; set; }

    public string? Target { get; set; }
}
