using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Plan.Entity;

/// <summary>
/// The sellable product (Docs/24): marketing content (merged from the old
/// website-only records, D1) plus four optional limitation axes. Each defined
/// axis is a base|cap pair — the buyer picks an integer multiple of the base
/// within [base, cap]; an undefined (NULL) axis is decided by the calculator
/// at purchase time, where a NULL output means unlimited (D2).
/// </summary>
/// <remarks>
/// Table name is the plural <c>Plans</c> — <c>PLAN</c> is a reserved T-SQL
/// keyword and the repository layer emits unbracketed SQL.
/// </remarks>
[Table("Plans")]
public class PlanEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Internal identifier, unique, admin-facing.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Display title shown to the buyer.</summary>
    public string Title { get; set; } = null!;

    public string? Caption { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// D1: the public home page lists active plans of the default group with
    /// this flag — private-group plans are never disclosed.
    /// </summary>
    public bool ShowOnWebsite { get; set; }

    public bool IsActive { get; set; } = true;

    public int GroupId { get; set; }

    public byte? UsersBase { get; set; }

    public byte? UsersCap { get; set; }

    public short? DaysBase { get; set; }

    public short? DaysCap { get; set; }

    /// <summary>Traffic axis base/cap in whole gigabytes.</summary>
    public int? TrafficBase { get; set; }

    public int? TrafficCap { get; set; }

    /// <summary>Bandwidth axis base/cap in megabits per second.</summary>
    public byte? RateBase { get; set; }

    public byte? RateCap { get; set; }

    public string CalculatorCode { get; set; } = null!;

    public DateTime Created { get; init; } = DateTime.Now;
}
