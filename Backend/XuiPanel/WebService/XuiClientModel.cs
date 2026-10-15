namespace PhotonBypass.Xui.WebService;

public class XuiClientModel
{
    public required string Email { get; set; }

    public required string Uuid { get; set; }

    public bool Enable { get; set; } = true;

    public long? TotalTrafficBytes { get; set; }

    public long? ExpiryTime { get; set; }

    public int LimitIp { get; set; }
}

public class XuiClientTrafficModel
{
    public required string Email { get; set; }

    public long Up { get; set; }

    public long Down { get; set; }

    public long Total => Up + Down;
}
