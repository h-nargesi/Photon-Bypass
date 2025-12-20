namespace PhotonBypass.Portal.Context;

public class RenewalContext
{
    public string? Target { get; set; }
    
    public byte? SimultaneousUserCount { get; set; }

    public short? Days { get; set; }

    public byte? Gigabytes { get; set; }
}
