namespace PhotonBypass.Application.Plan.Model;

/// <summary>Admin list row of one plan (Docs/24 §5).</summary>
public class PlanRowModel
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Caption { get; set; }

    public string? Description { get; set; }

    public bool ShowOnWebsite { get; set; }

    public bool IsActive { get; set; }

    public int GroupId { get; set; }

    public string GroupTitle { get; set; } = null!;

    public byte? UsersBase { get; set; }

    public byte? UsersCap { get; set; }

    public short? DaysBase { get; set; }

    public short? DaysCap { get; set; }

    public int? TrafficBase { get; set; }

    public int? TrafficCap { get; set; }

    public byte? RateBase { get; set; }

    public byte? RateCap { get; set; }

    public string CalculatorCode { get; set; } = null!;

    public List<int> RealmIds { get; set; } = [];
}

/// <summary>Create/update payload of a plan.</summary>
public class SavePlanModel
{
    public int? Id { get; set; }

    public string Name { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Caption { get; set; }

    public string? Description { get; set; }

    public bool ShowOnWebsite { get; set; }

    public bool IsActive { get; set; } = true;

    public int GroupId { get; set; }

    public byte? UsersBase { get; set; }

    public byte? UsersCap { get; set; }

    public short? DaysBase { get; set; }

    public short? DaysCap { get; set; }

    public int? TrafficBase { get; set; }

    public int? TrafficCap { get; set; }

    public byte? RateBase { get; set; }

    public byte? RateCap { get; set; }

    public string CalculatorCode { get; set; } = null!;

    public List<int> RealmIds { get; set; } = [];
}

/// <summary>Admin list row of one pricing group.</summary>
public class GroupRowModel
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public bool IsDefault { get; set; }

    public int MemberCount { get; set; }
}

/// <summary>Create/update payload of a pricing group.</summary>
public class SaveGroupModel
{
    public int? Id { get; set; }

    public string Title { get; set; } = null!;

    public bool IsDefault { get; set; }
}
