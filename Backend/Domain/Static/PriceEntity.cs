using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Static;

[Table("Price")]
public class PriceEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public PriceStates State { get; set; } = PriceStates.Visible;

    public string Title { get; set; } = null!;

    public string Caption { get; set; } = null!;

    public string Description { get; set; } = null!;

    public bool IsDefault { get; set; }

    public string CalculatorCode { get; set; } = null!;

    public DateTime Created { get; init; } = DateTime.Now;
}

public enum PriceStates
{
    Inactive = 0,
    Active = 1,
    Visible = 2,
}