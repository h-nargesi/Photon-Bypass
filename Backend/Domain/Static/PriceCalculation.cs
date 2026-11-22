namespace PhotonBypass.Domain.Static;

/// <summary>
/// One echoed plan axis for the calculator context (Docs/24 §4): the base|cap
/// pair as stored on the plan. <see cref="Base"/> null ⇒ the axis is undefined
/// on the plan and the calculator's output for it becomes the purchased value.
/// </summary>
public sealed class PlanAxis
{
    public int? Base { get; set; }

    public int? Cap { get; set; }
}

/// <summary>
/// Current state of the VPN account being renewed, read from the local
/// <c>PlanState</c> view (Docs/24 D3 — never a live realm call). Null on a
/// first purchase. NULL axis values mean "already unlimited".
/// </summary>
public sealed class CurrentAccountState
{
    public byte? SimultaneousUser { get; set; }

    public short? TimeLimitInDays { get; set; }

    /// <summary>Bytes; null = unlimited.</summary>
    public long? TrafficLimit { get; set; }

    public int? RateLimitInMeg { get; set; }

    public DateTime? ExpirationDate { get; set; }

    /// <summary>Bytes left on the running band; null = unlimited/no quota.</summary>
    public long? TrafficLeft { get; set; }

    public long TrafficUsed { get; set; }
}

/// <summary>
/// Input of the compiled calculator (Docs/24 D2): the purchase context —
/// buyer's choices for the plan-defined axes, the echoed plan axes, the group
/// title and the account's current state.
/// </summary>
public sealed class PriceCalculationInput
{
    public int PlanId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    /// <summary>Title of the buyer's pricing group.</summary>
    public string GroupTitle { get; set; } = string.Empty;

    /// <summary>Buyer's choice; null when the plan axis is undefined.</summary>
    public byte? Users { get; set; }

    /// <summary>Buyer's choice; null when the plan axis is undefined.</summary>
    public short? Days { get; set; }

    /// <summary>Buyer's choice in whole gigabytes; null when the plan axis is undefined.</summary>
    public int? Gigabytes { get; set; }

    /// <summary>Buyer's choice in Mbps; null when the plan axis is undefined.</summary>
    public int? RateLimitMeg { get; set; }

    public PlanAxis UsersAxis { get; set; } = new();

    public PlanAxis DaysAxis { get; set; } = new();

    public PlanAxis TrafficAxis { get; set; } = new();

    public PlanAxis RateAxis { get; set; } = new();

    /// <summary>Null on a first purchase.</summary>
    public CurrentAccountState? Current { get; set; }
}

/// <summary>
/// Output of the compiled calculator (Docs/24 D2). Every axis is always
/// present — null means unlimited. Values on plan-defined axes are ignored
/// (the validated buyer's choice stands); values on undefined axes become the
/// purchased limitation.
/// </summary>
public sealed class PriceCalculationOutput
{
    public int Price { get; set; }

    public byte? Users { get; set; }

    public short? Days { get; set; }

    public int? Gigabytes { get; set; }

    public int? RateLimitMeg { get; set; }
}
