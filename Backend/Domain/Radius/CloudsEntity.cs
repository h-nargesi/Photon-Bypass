using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Radius;

public class CloudsEntity : IBaseEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
