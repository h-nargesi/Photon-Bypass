using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account.Entity;

[Table("ResetPassword")]
public class ResetPassEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Panel user the code was issued for (docs/22 D11).</summary>
    public int AccountId { get; set; }

    public DateTime ExpireDate { get; set; }

    public string HashCode { get; init; } = null!;

    public DateTime Created { get; init; } = DateTime.Now;
}
