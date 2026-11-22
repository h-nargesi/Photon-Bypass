namespace PhotonBypass.Application.Plan.Model;

/// <summary>
/// One purchasable plan of the acting group (Docs/24 §5): display title, the
/// base|cap axis pairs driving the choice widgets, and the plan's realm
/// choices intersected with the capacity filter.
/// </summary>
public class PlanCatalogModel
{
    public int Id { get; init; }

    public string Title { get; init; } = null!;

    public byte? UsersBase { get; init; }

    public byte? UsersCap { get; init; }

    public short? DaysBase { get; init; }

    public short? DaysCap { get; init; }

    public int? TrafficBase { get; init; }

    public int? TrafficCap { get; init; }

    public byte? RateBase { get; init; }

    public byte? RateCap { get; init; }

    public List<RealmChoiceModel> Realms { get; init; } = [];
}
