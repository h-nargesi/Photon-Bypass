using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.API.Context;

public class EstimateContext
{
    public int? SimultaneousUserCount { get; set; }
    
    public int? Months { get; set; }
    
    public int? Gigabytes { get; set; }
}
