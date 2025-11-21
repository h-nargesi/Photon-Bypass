using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Static;

namespace PhotonBypass.Test.BasicFunctions.Infrastructure;

public class PriceCalculatorTest : ServiceInitializer
{
    protected override void AddServices(IHostApplicationBuilder builder)
    {
        var price_repository = new Mock<IPriceRepository>();
        price_repository.Setup(x => x.GetLatest())
            .Returns(Task.FromResult(Data));
        builder.Services.AddSingleton(price_repository.Object);
    }

    [Fact]
    public void CalculatePrice_ShouldCompileAndCalculate_Traffic_5_100()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var result = calculator.CalculatePrice(PlanType.Traffic, 5, 100 / 25);
        Assert.Equal(310, result);
    }

    [Fact]
    public void CalculatePrice_ShouldCompileAndCalculate_Monthly_5_3()
    {
        using var scope = App.Services.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();
        var result = calculator.CalculatePrice(PlanType.Monthly, 5, 3);
        Assert.Equal(1980, result);
    }

    private static readonly IList<PriceEntity> Data =
    [
        new() {
            PlanType = PlanType.Traffic,
            CalculatorCode = """
using System;

public class Calculator
{
 public static int Compute(int users, int traffic)
 {
     return 60 + users * 10 + traffic * 50;
 }
}
""",
        },
        new() {
            PlanType = PlanType.Monthly,
            CalculatorCode = """
using System;

public class Calculator
{
    public static int Compute(int users, int count)
    {
        var month = 190;
        if (users >= 2) month += 150;
        if (users >= 3) month += 120;
        if (users >= 4) month += 100 * (users - 3);
        return count * month;
    }
}
""",
        }
    ];
}
