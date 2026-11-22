using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.Facts.BasicFunctions.Business;

/// <summary>
/// Docs/24 §2/§6: the multiple-of-25/30 rules and the days-derivation are
/// gone — choices are validated against the plan's base|cap axes, undefined
/// axes must stay null, and a composed purchase needs at least one limit.
/// </summary>
public class RenewalBusinessTest
{
    private static PlanEntity CompletePlan()
    {
        return new PlanEntity
        {
            Name = "Complete",
            Title = "کامل",
            UsersBase = 1,
            UsersCap = 6,
            DaysBase = 30,
            DaysCap = 180,
            TrafficBase = 25,
            TrafficCap = 200,
        };
    }

    private static PlanEntity TrafficOnlyPlan()
    {
        return new PlanEntity
        {
            Name = "Trafiki",
            Title = "ترافیکی",
            TrafficBase = 25,
            TrafficCap = 300,
        };
    }

    [Fact]
    public void ValidateChoices_DefinedAxisWithoutChoice_Fails()
    {
        var plan = TrafficOnlyPlan();

        var has_error = plan.ValidateChoices(null, null, null, null, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void ValidateChoices_ChoiceNotAMultipleOfBase_Fails()
    {
        var plan = TrafficOnlyPlan();

        var has_error = plan.ValidateChoices(null, null, 23, null, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void ValidateChoices_ChoiceAboveCap_Fails()
    {
        var plan = TrafficOnlyPlan();

        var has_error = plan.ValidateChoices(null, null, 325, null, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void ValidateChoices_ChoiceOnUndefinedAxis_Fails()
    {
        var plan = TrafficOnlyPlan();

        var has_error = plan.ValidateChoices(null, 60, 50, null, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void ValidateChoices_ValidMultipleWithinRange_Passes()
    {
        var plan = TrafficOnlyPlan();

        var has_error = plan.ValidateChoices(null, null, 75, null, out var user_exception);

        Assert.False(has_error);
        Assert.Null(user_exception);
    }

    [Fact]
    public void ValidateChoices_AllDefinedAxesValid_Passes()
    {
        var plan = CompletePlan();

        var has_error = plan.ValidateChoices(2, 120, 50, null, out var user_exception);

        Assert.False(has_error);
        Assert.Null(user_exception);
    }

    [Fact]
    public void ValidateChoices_BaseMultipleBelowBase_Fails()
    {
        var plan = CompletePlan();

        // 0 is technically a multiple — but below the base
        var has_error = plan.ValidateChoices(0, 120, 50, null, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void ValidateDefinition_RequiresAtLeastOneAxis()
    {
        var plan = new PlanEntity { Name = "Empty", Title = "خالی" };

        var has_error = plan.ValidateDefinition(out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void ValidateDefinition_RejectsBaseWithoutCap()
    {
        var plan = new PlanEntity { Name = "Half", Title = "نیمه", UsersBase = 1 };

        var has_error = plan.ValidateDefinition(out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void ValidateDefinition_RejectsCapBelowBase()
    {
        var plan = new PlanEntity { Name = "Inverted", Title = "وارونه", UsersBase = 5, UsersCap = 2 };

        var has_error = plan.ValidateDefinition(out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void HasNoLimit_AllUnlimitedFails()
    {
        var has_error = RenewalBusiness.HasNoLimit(null, null, null, null, out var user_exception);

        Assert.True(has_error);
        Assert.NotNull(user_exception);
    }

    [Fact]
    public void HasNoLimit_AnyLimitedAxisPasses()
    {
        Assert.False(RenewalBusiness.HasNoLimit(null, 30, null, null, out var e1));
        Assert.Null(e1);

        Assert.False(RenewalBusiness.HasNoLimit(2, null, null, null, out var e2));
        Assert.Null(e2);

        Assert.False(RenewalBusiness.HasNoLimit(null, null, 25 * StaticValues.BytesInGigLong, null, out var e3));
        Assert.Null(e3);

        Assert.False(RenewalBusiness.HasNoLimit(null, null, null, 8, out var e4));
        Assert.Null(e4);
    }

    [Fact]
    public void GetPlanTitle_NullUsers_ShowsUnlimited()
    {
        var renewal = new RenewalEntity
        {
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 90,
            SimultaneousUser = null,
        };

        var title = renewal.GetPlanTitle();

        Assert.DoesNotContain("کاربره", title);
        Assert.Contains("نامحدود", title);
    }

    [Fact]
    public void GetPlanTitle_LimitedUsers_ShowsTheCount()
    {
        var renewal = new RenewalEntity
        {
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 90,
            SimultaneousUser = 2,
        };

        var title = renewal.GetPlanTitle();

        Assert.Contains("2 کاربره", title);
    }
}
