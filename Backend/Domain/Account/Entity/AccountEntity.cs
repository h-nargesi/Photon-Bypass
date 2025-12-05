using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Entity;

[Table("Account")]
public class AccountEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public bool Active { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string VpnPassword { get; set; } = null!;

    public int? Parent { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string Fullname => (Name + " " + Surname).Trim();

    public string? Mobile { get; set; }

    public bool MobileValid { get; set; }

    public string? Email { get; set; }

    public bool EmailValid { get; set; }

    public int Balance { get; set; }

    public int? CalculationMethod { get; set; }

    public DateTime? WarningTimes { get; set; }

    public bool SendWarning { get; set; }

    public string? Picture { get; set; }

    public UserTypes UserType { get; set; }

    public int? ReferenceId { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;
}
