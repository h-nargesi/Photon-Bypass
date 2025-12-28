using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Entity;

[Table("Wallet")]
public class WalletEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int Amount { get; set; }

    public BalanceDirection Direction { get; set; }

    public BalanceStatus Status { get; set; } = BalanceStatus.Pending;

    public string Description { get; set; } = null!;

    public int? InvoiceCode { get; set; }

    public string? ReferenceCode { get; set; }

    public string? Action { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;

    [NotMapped]
    public int RenewId { get; set; }
}