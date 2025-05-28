using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Services;

namespace PhotonBypass.Test.Infrastructure;

public class PriceCalculatorTest
{
    readonly Mock<IPriceRepository> _priceRepository;
    readonly PriceCalculator _calculator;

    public PriceCalculatorTest()
    {
        _priceRepository = new Mock<IPriceRepository>();
        _priceRepository.Setup(x => x.GetLeatest())
            .Returns(Task.FromResult(Data));
        _calculator = new PriceCalculator(_priceRepository.Object);
    }

    [Fact]
    public void CalculatePrice_ShouldCompileAndCalculate_Traffic_5_100()
    {
        var result = _calculator.CalculatePrice(PlanType.Traffic, 5, 100 / 25);
        Assert.Equal(310, result);
    }

    [Fact]
    public void CalculatePrice_ShouldCompileAndCalculate_Monthly_5_3()
    {
        var result = _calculator.CalculatePrice(PlanType.Monthly, 5, 3);
        Assert.Equal(1980, result);
    }

    static readonly IList<PriceEntity> Data =
    [
        new() {
            PlanType = PlanType.Traffic,
            CalculatorCode = @"
using System;

public class Calculator
{
    public static int Compute(int users, int traffic)
    {
        return 60 + users * 10 + traffic * 50;
    }
}",
        },
        new() {
            PlanType = PlanType.Monthly,
            CalculatorCode = @"
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
}",
        }
    ];
}
