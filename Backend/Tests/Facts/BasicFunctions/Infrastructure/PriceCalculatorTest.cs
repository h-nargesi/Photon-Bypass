using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.BasicFunctions.Infrastructure;

/// <summary>
/// Docs/24 D2/D4: the calculators implement the closed
/// <c>Compute(PriceCalculationInput) → PriceCalculationOutput</c> contract,
/// the pool is keyed by plan id, and a plan save (entity event) swaps the
/// generation atomically.
/// </summary>
public class PriceCalculatorTest : UnitLevelServiceInitializer
{
    private static PriceCalculationInput MakeInput(int plan_id, byte? users = null, short? days = null, int? gigabytes = null)
    {
        return new PriceCalculationInput
        {
            PlanId = plan_id,
            PlanName = $"plan-{plan_id}",
            GroupTitle = "وب سایت",
            Users = users,
            Days = days,
            Gigabytes = gigabytes,
        };
    }

    [Fact]
    public async Task Calculate_ShouldCompileAndCalculate_Trafiki_100G()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        var result = await calculator.Calculate(MakeInput(1, gigabytes: 100));

        // 60 + (100/25) * 50 — the traffic-only plan prices without any user choice
        Assert.Equal(260, result.Price);

        // the calculator decides the undefined axes: effective days, unlimited users
        Assert.Equal((short)180, result.Days);
        Assert.Null(result.Users);
    }

    [Fact]
    public async Task Calculate_ShouldCompileAndCalculate_Economy_125G()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        var result = await calculator.Calculate(MakeInput(2, gigabytes: 125));

        // 40 + (125/25) * 40
        Assert.Equal(240, result.Price);
        Assert.Equal((byte)2, result.Users);
    }

    [Fact]
    public async Task Calculate_CompletePlan_EchoesTheChosenAxes()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        var result = await calculator.Calculate(MakeInput(3, users: 2, days: 120, gigabytes: 50));

        // 30 + 2*5 + 120*2 + (50/25)*10
        Assert.Equal(300, result.Price);
        Assert.Equal((byte)2, result.Users);
        Assert.Equal((short)120, result.Days);
        Assert.Equal(50, result.Gigabytes);
    }

    [Fact]
    public async Task Calculate_UnknownPlanId_Throws()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        // the closed contract has no silent fallback (Docs/24 D2)
        await Assert.ThrowsAsync<Exception>(() => calculator.Calculate(MakeInput(4_444, gigabytes: 25)));
    }

    [Fact]
    public async Task Calculate_ConcurrentFirstUse_InitializesOnce()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => calculator.Calculate(MakeInput(1, gigabytes: 100))));

        Assert.All(results, result => Assert.Equal(260, result.Price));
    }

    [Fact]
    public async Task Calculate_RefreshOnPlanSave_SwapsPoolAndKeepsWorking()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

        Assert.Equal(260, (await calculator.Calculate(MakeInput(1, gigabytes: 100))).Price);

        // D4: repository saves raise the entity event — the pool rebuilds
        await event_service.CallOnSave(this, new EntityEventArgs<PlanEntity>(new PlanEntity { Id = 1 }));

        Assert.Equal(260, (await calculator.Calculate(MakeInput(1, gigabytes: 100))).Price);
        Assert.Equal(240, (await calculator.Calculate(MakeInput(2, gigabytes: 125))).Price);
    }

    [Fact]
    public async Task Reload_RebuildsThePoolForOutOfBandEdits()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        Assert.Equal(260, (await calculator.Calculate(MakeInput(1, gigabytes: 100))).Price);

        // D4: the explicit admin path — direct SQL edits raise no event
        await calculator.Reload();

        Assert.Equal(260, (await calculator.Calculate(MakeInput(1, gigabytes: 100))).Price);
    }

    [Fact]
    public void ValidateCode_RejectsASnippetWithoutTheClosedContract()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        Assert.Throws<Exception>(() => calculator.ValidateCode(
            "public class Calculator { public static int Compute(int users, int days, int gigabytes) => 0; }"));

        calculator.ValidateCode(
            "using PhotonBypass.Domain.Static; public class Calculator { public static PriceCalculationOutput Compute(PriceCalculationInput input) => new PriceCalculationOutput { Price = 1 }; }");
    }
}
