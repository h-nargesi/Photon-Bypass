using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.User;

public class PermenantUserEntity : IBaseEntity
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool Active { get; set; }

    public string Realm { get; set; } = null!;

    public int Realm_id { get; set; }

    public string Profile { get; set; } = null!;

    public int Profile_id { get; set; }

    public DateTime? From_date { get; set; }

    public DateTime? To_date { get; set; }

    public int Cloud_id { get; set; }
}
