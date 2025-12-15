using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.Portal.Context;

public class EstimateContext
{
    public byte? SimultaneousUserCount { get; set; }
    
    public short? Days { get; set; }
    
    public byte? Gigabytes { get; set; }
}
