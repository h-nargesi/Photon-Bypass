using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.User;

public class AccountEntity : IBaseEntity
{
    public int Id { get; set; }

    public bool Active { get; set; }

    public int CloudId { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string Fullname => (Name + " " + Surname).Trim();

    public string? Mobile { get; set; }

    public bool MobileValid { get; set; }

    public string? Email { get; set; }

    public bool EmailValid { get; set; }

    public decimal Balance { get; set; }

    public string? Picture { get; set; }

    public int? Parent { get; set; }
}
