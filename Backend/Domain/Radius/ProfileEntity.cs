using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Radius;

public class ProfileEntity : IBaseEntity
{
    public int Id { get; set; }

    public int Cloud_id { get; set; }

    public string Name { get; set; } = null!;

    public int? Simultaneous_Use { get; set; }

    public string? Mikrotik_Rate_Limit { get; set; }

    public string? Rd_Reset_Type_Data { get; set; }
}
