using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Entity;

[Table("Account")]
public class AccountEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public bool IsActive { get; set; }

    public string Username { get; init; } = null!;

    public string Password { get; set; } = null!;

    public int? Owner { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;

    // Account Perosanl Info

    public string? Name { get; set; }

    public string? Surname { get; set; }

    [NotMapped]
    public string Fullname => (Name + " " + Surname).Trim();

    public string? Mobile { get; set; }

    public bool IsMobileValid { get; set; }

    [NotMapped]
    public string? MobileNumber => IsMobileValid ? Mobile : null;

    public string? Email { get; set; }

    public bool IsEmailValid { get; set; }

    [NotMapped]
    public string? EmailAddress => IsEmailValid ? Email : null;

    [NotMapped]
    public string? Picture => AccountBusiness.PicturePath + Id;

    // Profile Info

    public int Balance { get; set; }

    public int? CalculationMethod { get; set; }

    public UserTypes UserType { get; set; }

    public string VpnPassword { get; set; } = null!;

    public DateTime? LastWarningTime { get; set; }

    public bool SendWarning { get; set; } = true;
}
