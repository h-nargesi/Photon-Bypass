using PhotonBypass.Domain.Static;

namespace PhotonBypass.Test.BasicFunctions.Infrastructure;

public class PriceCalculatorTest : ServiceInitializer
{
    [Fact]
    public void CalculatePrice_ShouldCompileAndCalculate_Public_5_100()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var result = calculator.CalculatePrice(1, 5, 120, 100);
        Assert.Equal(310, result);
    }

    [Fact]
    public void CalculatePrice_ShouldCompileAndCalculate_Friends_5_125()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var result = calculator.CalculatePrice(2, 5, 90, 125);
        Assert.Equal(290, result);
    }
}