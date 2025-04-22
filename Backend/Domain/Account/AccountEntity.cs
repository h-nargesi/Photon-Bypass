using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account;

[Table("Account")]
public class AccountEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public bool Active { get; set; }

    public int CloudId { get; set; }

    public int PermanentUserId { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string Fullname => (Name + " " + Surname).Trim();

    public string? Mobile { get; set; }

    public bool MobileValid { get; set; }

    public string? Email { get; set; }

    public bool EmailValid { get; set; }

    public int Balance { get; set; }

    public string? Picture { get; set; }

    public int? Parent { get; set; }
}
