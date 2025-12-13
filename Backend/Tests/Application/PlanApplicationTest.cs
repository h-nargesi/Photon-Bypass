using FluentAssertions;
using PhotonBypass.Application.Plan;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Test.Application;

public class PlanApplicationTest : ServiceInitializer
{
    [Fact]
    public async Task GetPlanState_InvalidUsername()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanState("Invalid username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanState_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanState("InactiveUser7");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanState_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanState("User6");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanState_User2()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var plan_state = await plan_app.GetPlanState("User2");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.SimultaneousUserCount);
        Assert.Null(plan_state.Data.RemainsTrafficPercent);
        Assert.Null(plan_state.Data.RemainsTimePercent);
    }

    [Fact]
    public async Task GetPlanState_User1()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var plan_state = await plan_app.GetPlanState("User1");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.SimultaneousUserCount);
        Assert.Equal(99, plan_state.Data.RemainsTrafficPercent);
        Assert.Equal(91, plan_state.Data.RemainsTimePercent);
    }
}
