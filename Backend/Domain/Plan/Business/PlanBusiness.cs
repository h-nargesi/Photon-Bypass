using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Domain.Plan.Business;

/// <summary>
/// Axis algebra of the plan model (Docs/24 §2/§6): a defined axis is a
/// base|cap pair, the buyer must pick an integer multiple of the base within
/// [base, cap], and an undefined axis is decided by the calculator (null
/// output = unlimited). This replaces the old multiple-of-25/30 rules and the
/// days-derivation formula.
/// </summary>
public static class PlanBusiness
{
    /// <summary>Wire titles of the four limitation axes.</summary>
    public const string UsersAxisTitle = "تعداد اتصالات همزمان";

    public const string DaysAxisTitle = "مدت";

    public const string TrafficAxisTitle = "حجم ترافیک";

    public const string RateAxisTitle = "پهنای باند";

    public readonly record struct AxisDescriptor(string Title, long? Base, long? Cap)
    {
        public bool IsDefined => Base.HasValue;
    }

    public static AxisDescriptor UsersAxis(this PlanEntity plan) => new(UsersAxisTitle, plan.UsersBase, plan.UsersCap);

    public static AxisDescriptor DaysAxis(this PlanEntity plan) => new(DaysAxisTitle, plan.DaysBase, plan.DaysCap);

    public static AxisDescriptor TrafficAxis(this PlanEntity plan) => new(TrafficAxisTitle, plan.TrafficBase, plan.TrafficCap);

    public static AxisDescriptor RateAxis(this PlanEntity plan) => new(RateAxisTitle, plan.RateBase, plan.RateCap);

    public static IEnumerable<AxisDescriptor> Axes(this PlanEntity plan)
    {
        yield return plan.UsersAxis();
        yield return plan.DaysAxis();
        yield return plan.TrafficAxis();
        yield return plan.RateAxis();
    }

    /// <summary>
    /// Save-time plan sanity (mirrors the CHECK constraints): both-or-neither
    /// per axis, base ≥ 1, cap ≥ base, and at least one defined axis.
    /// </summary>
    public static bool ValidateDefinition(this PlanEntity plan, out UserException error)
    {
        foreach (var axis in plan.Axes())
        {
            if (axis.Base.HasValue != axis.Cap.HasValue)
            {
                error = new UserException($"پایه و سقف محدودیت «{axis.Title}» باید با هم تعیین شوند!",
                    $"Plan axis pair incomplete: {axis.Title}, base={axis.Base}, cap={axis.Cap}");
                return true;
            }

            if (axis.Base is < 1)
            {
                error = new UserException($"پایه محدودیت «{axis.Title}» باید حداقل ۱ باشد!",
                    $"Plan axis base below one: {axis.Title}, base={axis.Base}");
                return true;
            }

            if (axis.Base.HasValue && axis.Cap.HasValue && axis.Cap.Value < axis.Base.Value)
            {
                error = new UserException($"سقف محدودیت «{axis.Title}» نمی‌تواند کمتر از پایه آن باشد!",
                    $"Plan axis cap below base: {axis.Title}, base={axis.Base}, cap={axis.Cap}");
                return true;
            }
        }

        if (!plan.Axes().Any(axis => axis.IsDefined))
        {
            error = new UserException("پلن باید حداقل یک محدودیت تعیین‌شده داشته باشد!",
                "Plan without any defined axis (Docs/24 §6: at least one limit).");
            return true;
        }

        error = null!;
        return false;
    }

    /// <summary>
    /// Purchase-time choice check: every plan-defined axis carries an integer
    /// multiple of its base within [base, cap]; undefined axes must stay null
    /// (the calculator decides them). Runs before the calculator call —
    /// cheap and fail-fast (Docs/24 §6).
    /// </summary>
    public static bool ValidateChoices(this PlanEntity plan, byte? users, short? days, int? gigabytes, int? rate,
        out UserException error)
    {
        if (CheckAxis(plan.UsersAxis(), users, out error) ||
            CheckAxis(plan.DaysAxis(), days, out error) ||
            CheckAxis(plan.TrafficAxis(), gigabytes, out error) ||
            CheckAxis(plan.RateAxis(), rate, out error))
        {
            return true;
        }

        error = null!;
        return false;
    }

    /// <summary>Returns true (with the error) when the axis choice violates the plan definition.</summary>
    private static bool CheckAxis(in AxisDescriptor axis, long? value, out UserException error)
    {
        if (!axis.IsDefined)
        {
            if (value.HasValue)
            {
                error = new UserException($"پلن انتخابی محدودیت «{axis.Title}» ندارد!",
                    $"Choice sent for an undefined plan axis: {axis.Title}, value={value}");
                return true;
            }

            error = null!;
            return false;
        }

        if (!value.HasValue)
        {
            error = new UserException($"مقدار «{axis.Title}» انتخاب نشده است!",
                $"Missing choice for a defined plan axis: {axis.Title}");
            return true;
        }

        if (value.Value < axis.Base!.Value || value.Value > axis.Cap!.Value)
        {
            error = new UserException(
                $"مقدار «{axis.Title}» باید بین {axis.Base} و {axis.Cap} باشد!",
                $"Plan axis choice out of range: {axis.Title}, value={value}, base={axis.Base}, cap={axis.Cap}");
            return true;
        }

        if (value.Value % axis.Base.Value != 0)
        {
            error = new UserException(
                $"مقدار «{axis.Title}» باید مضرب صحیحی از {axis.Base} باشد!",
                $"Plan axis choice is not an integer multiple of the base: {axis.Title}, value={value}, base={axis.Base}");
            return true;
        }

        error = null!;
        return false;
    }
}
