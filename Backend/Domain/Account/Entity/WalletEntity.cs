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
    
    public BalanceStatus Status { get; set; }

    public string Description { get; set; } = null!;
    
    public string? ReferenceCode { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}