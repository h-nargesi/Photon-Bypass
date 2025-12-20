using FluentAssertions;
using PhotonBypass.Application.Plan;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Application;

public class PlanApplicationTest : UnitLevelServiceInitializer
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
        Assert.Equal(92, plan_state.Data.RemainsTimePercent); // real value is 92.5%
    }

    [Fact]
    public async Task GetPlanInfo_InvalidUsername()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanInfo("Invalid username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanInfo_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanInfo("InactiveUser7");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanInfo_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var plan_state = await plan_app.GetPlanInfo("User6");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal("User6", plan_state.Data.Target);
        Assert.Null(plan_state.Data.SimultaneousUserCount);
        Assert.Null(plan_state.Data.Days);
        Assert.Null(plan_state.Data.Gigabytes);
    }

    [Fact]
    public async Task GetPlanInfo_User1()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var plan_state = await plan_app.GetPlanInfo("User1");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.SimultaneousUserCount);
        Assert.Equal(120, plan_state.Data.Days);
        Assert.Equal(1, plan_state.Data.Gigabytes);
    }

    [Fact]
    public async Task Renewal_InvalidUsername()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var func = () => plan_app.Renewal("InvalidUser", 2, 10, 20);
        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var func = () => plan_app.Renewal("InactiveUser7", 2, 10, 20);
        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var estimate = await plan_app.Estimate("User2", 2, 120, 25);

        Assert.NotNull(estimate);

        var result = await plan_app.Renewal("User2", 2, 120, 25);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(1500 - estimate.Data?.Price, result.Data.CurrentPrice);
        Assert.Equal(0, result.Data.MoneyNeeds);
    }

    [Fact]
    public async Task Renewal_EmptyMoney()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var estimate = await plan_app.Estimate("User3", 2, 120, 25);

        Assert.NotNull(estimate);

        var result = await plan_app.Renewal("User3", 2, 120, 25);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.CurrentPrice);
        Assert.Equal(estimate.Data?.Price, result.Data.MoneyNeeds);
    }
}
