using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Static;

[Table("Price")]
public class PriceEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public bool IsActive { get; set; }

    public string Title { get; set; } = null!;

    public string Caption { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string CalculatorCode { get; set; } = null!;
}
