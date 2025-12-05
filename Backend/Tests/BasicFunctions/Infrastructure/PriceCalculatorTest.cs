using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Static;

namespace PhotonBypass.Test.BasicFunctions.Infrastructure;

public class PriceCalculatorTest : ServiceInitializer
{
    protected override void AddServices(IHostApplicationBuilder builder)
    {
        var price_repository = new Mock<IPriceRepository>();
        price_repository.Setup(x => x.GetActives())
            .Returns(Task.FromResult(Data));
        builder.Services.AddSingleton(price_repository.Object);
    }

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

    private static readonly IList<PriceEntity> Data =
    [
        new()
        {
            Id = 1,
            CalculatorCode = """
                             using System;
                             
                             public class Calculator
                             {
                                 public static int Compute(int users, int days, int gigabytes)
                                 {
                                     return 60 + users * 10 + (gigabytes / 25) * 50;
                                 }
                             }
                             """,
        },
        new()
        {
            Id = 2,
            CalculatorCode = """
                             using System;

                             public class Calculator
                             {
                                 public static int Compute(int users, int days, int gigabytes)
                                 {
                                    return 40 + users * 10 + (gigabytes / 25) * 40;
                                 }
                             }
                             """,
        }
    ];
}