using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using Microsoft.Extensions.Options;
using PhotonBypass.Application.Basics;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

public class BasicsApplicationTest : UnitLevelServiceInitializer
{
    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        // the default moqs are transient, so a setup made on the instance
        // resolved by the test would not be visible to BasicsApplication;
        // re-registering them as scoped makes both share one mock per scope
        builder.Services.AddScoped<PlanRepositoryMoq>();
        builder.Services.AddScoped<UserGroupRepositoryMoq>();
    }

    [Fact]
    public async Task GetPrices_MapsWebsitePlansOfTheDefaultGroup_WithSplitDescriptionLines()
    {
        using var scope = App.Services.CreateScope();
        var basics = scope.ServiceProvider.GetRequiredService<IBasicsApplication>();
        var plan_moq = scope.ServiceProvider.GetRequiredService<PlanRepositoryMoq>();

        plan_moq.Setup(repo => repo.GetWebsitePlans(It.IsAny<int>())).ReturnsAsync(
        [
            new PlanEntity
            {
                Title = "Plan A",
                Caption = "Best value",
                Description = "line one\nline two",
                ShowOnWebsite = true,
            },
            new PlanEntity
            {
                Title = "Plan B",
                Caption = "Cheap",
                Description = "single line",
                ShowOnWebsite = true,
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
    public async Task GetPrices_WithoutWebsitePlans_ReturnsEmptyList()
    {
        using var scope = App.Services.CreateScope();
        var basics = scope.ServiceProvider.GetRequiredService<IBasicsApplication>();
        var plan_moq = scope.ServiceProvider.GetRequiredService<PlanRepositoryMoq>();

        plan_moq.Setup(repo => repo.GetWebsitePlans(It.IsAny<int>())).ReturnsAsync([]);

        var result = await basics.GetPrices();

        result.Code.Should().Be(200);
        result.Data.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAppConfig_MirrorsRegistrationFlag(bool allow_registration)
    {
        using var scope = App.Services.CreateScope();
        var basics = scope.ServiceProvider.GetRequiredService<IBasicsApplication>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ManagementOptions>>();

        options.Value.AllowRegistration = allow_registration;

        var result = await basics.GetAppConfig();

        result.Code.Should().Be(200);
        result.Data!.AllowRegistration.Should().Be(allow_registration);
    }
}
