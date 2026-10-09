using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.BasicFunctions.Infrastructure;

public class PriceCalculatorTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task CalculatePrice_ShouldCompileAndCalculate_Public_5_100()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var result = await calculator.CalculatePrice(1, 5, 120, 100);
        Assert.Equal(310, result);
    }

    [Fact]
    public async Task CalculatePrice_ShouldCompileAndCalculate_Friends_5_125()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var result = await calculator.CalculatePrice(2, 5, 90, 125);
        Assert.Equal(290, result);
    }

    [Fact]
    public async Task CalculatePrice_ConcurrentFirstUse_InitializesOnce()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => calculator.CalculatePrice(1, 5, 120, 100)));

        Assert.All(results, result => Assert.Equal(310, result));
    }

    [Fact]
    public async Task CalculatePrice_RefreshOnPriceSave_SwapsPoolAndKeepsWorking()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var event_service = scope.ServiceProvider.GetRequiredService<IEntityEventService>();

        Assert.Equal(310, await calculator.CalculatePrice(1, 5, 120, 100));

        await event_service.CallOnSave(this, new EntityEventArgs<PriceEntity>(new PriceEntity { Id = 1 }));

        Assert.Equal(310, await calculator.CalculatePrice(1, 5, 120, 100));
        Assert.Equal(290, await calculator.CalculatePrice(2, 5, 90, 125));
    }
}
