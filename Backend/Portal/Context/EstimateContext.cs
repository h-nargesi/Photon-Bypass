using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.Portal.Context;

public class EstimateContext
{
    public int? SimultaneousUserCount { get; set; }
    
    public int? Days { get; set; }
    
    public int? Gigabytes { get; set; }
}
