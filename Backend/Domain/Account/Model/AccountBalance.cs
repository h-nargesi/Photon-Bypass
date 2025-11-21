using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account.Model;

[Table("AccountBalance")]
public class AccountBalance
{
    [Key]
    public int Id { get; set; }

    public bool Active { get; set; }

    public int CloudId { get; set; }

    public int Balance { get; set; }
}