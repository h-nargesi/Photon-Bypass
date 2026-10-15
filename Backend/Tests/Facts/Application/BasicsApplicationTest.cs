using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Basics;
using PhotonBypass.Domain.Static;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

public class BasicsApplicationTest : UnitLevelServiceInitializer
{
    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        // the default PriceRepositoryMoq is transient, so a setup made on the
        // instance resolved by the test would not be visible to BasicsApplication;
        // re-registering it as scoped makes both share one mock per scope
        builder.Services.AddScoped<PriceRepositoryMoq>();
    }

    [Fact]
    public async Task GetPrices_MapsVisiblePrices_WithSplitDescriptionLines()
    {
        using var scope = App.Services.CreateScope();
        var basics = scope.ServiceProvider.GetRequiredService<IBasicsApplication>();
        var price_moq = scope.ServiceProvider.GetRequiredService<PriceRepositoryMoq>();

        price_moq.Setup(repo => repo.GetVisibles()).ReturnsAsync(
        [
            new PriceEntity
            {
                Title = "Plan A",
                Caption = "Best value",
                Description = "line one\nline two",
            },
            new PriceEntity
            {
                Title = "Plan B",
                Caption = "Cheap",
                Description = "single line",
            },
        ]);

        var result = await basics.GetPrices();

        result.Code.Should().Be(200);
        result.Data.Should().HaveCount(2);

        var first = result.Data![0];
        first.Title.Should().Be("Plan A");
        first.Caption.Should().Be("Best value");
        first.Description.Should().Equal("line one", "line two");

        result.Data![1].Description.Should().ContainSingle().Which.Should().Be("single line");
    }

    [Fact]
    public async Task GetPrices_WithoutVisiblePrices_ReturnsEmptyList()
    {
        using var scope = App.Services.CreateScope();
        var basics = scope.ServiceProvider.GetRequiredService<IBasicsApplication>();
        var price_moq = scope.ServiceProvider.GetRequiredService<PriceRepositoryMoq>();

        price_moq.Setup(repo => repo.GetVisibles()).ReturnsAsync([]);

        var result = await basics.GetPrices();

        result.Code.Should().Be(200);
        result.Data.Should().BeEmpty();
    }
}
