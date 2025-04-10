namespace PhotonBypass.Domain.Model;

public class UserRadEntity
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool Active { get; set; }

    public string Realm { get; set; } = string.Empty;

    public int Realm_id { get; set; }

    public string Profile { get; set; } = string.Empty;

    public int Profile_id { get; set; }

    public DateTime? From_date { get; set; }

    public DateTime? To_date { get; set; }

    public int Cloud_id { get; set; }
}
