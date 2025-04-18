using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account;

[Table("ResetPassword")]
public class ResetPassEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public DateTime ExpireDate { get; set; }

    public string HashCode { get; set; } = null!;
}
