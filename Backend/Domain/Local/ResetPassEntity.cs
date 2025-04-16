using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Local;

[Table("ResetPassword")]
public class ResetPassEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public DateTime ExpireDate { get; set; }

    public string HashCode { get; set; } = null!;
}
