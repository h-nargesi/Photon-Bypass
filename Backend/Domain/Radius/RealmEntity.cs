using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Radius;

public class RealmEntity : IBaseEntity
{
    public int Id { get; set; }

    public int Cloud_id { get; set; }

    public string Name { get; set; } = null!;

    public string Suffix { get; set; } = null!;
}
