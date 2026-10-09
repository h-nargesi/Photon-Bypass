using Microsoft.Extensions.Options;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

public class WalletThresholdServiceTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task Disabled_WhenThresholdIsNull()
    {
        using var scope = App.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ManagementOptions>>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        options.Value.WalletDeactivationThreshold = null;

        var service = scope.ServiceProvider.GetRequiredService<IWalletThresholdService>();

        await service.CheckAndApply();

        Assert.Empty(history_moq.Data);
    }

    [Fact]
    public async Task Below_ThenAbove_AppliesAndReverts()
    {
        using var scope = App.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ManagementOptions>>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        options.Value.WalletDeactivationThreshold = 0;

        var service = scope.ServiceProvider.GetRequiredService<IWalletThresholdService>();

        await service.CheckAndApply();

        var deactivated = history_moq.Data
            .Where(h => h.Title == IWalletThresholdService.HistoryTitle && h.Target == 3)
            .ToList();

        Assert.Single(deactivated);
        Assert.Equal("deactivated", deactivated[0].Value);

        await service.CheckAndApply();

        Assert.Single(history_moq.Data.Where(h => h.Target == 3));

        await wallet_repo.Save(new WalletEntity
        {
            AccountId = 3,
            Amount = 1000,
            Direction = BalanceDirection.Credit,
            Status = BalanceStatus.Completed,
            Description = "charge",
        });

        await service.CheckAndApply();

        var states = history_moq.Data
            .Where(h => h.Title == IWalletThresholdService.HistoryTitle && h.Target == 3)
            .OrderBy(h => h.Id)
            .ToList();

        Assert.Equal(2, states.Count);
        Assert.Equal("activated", states[1].Value);

        await service.CheckAndApply();

        Assert.Equal(2, history_moq.Data.Count(h => h.Title == IWalletThresholdService.HistoryTitle && h.Target == 3));
    }

    [Fact]
    public async Task AboveThreshold_NoHistory()
    {
        using var scope = App.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ManagementOptions>>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        options.Value.WalletDeactivationThreshold = -5000;

        var service = scope.ServiceProvider.GetRequiredService<IWalletThresholdService>();

        await service.CheckAndApply();

        Assert.Empty(history_moq.Data.Where(h => h.Title == IWalletThresholdService.HistoryTitle));
    }
}
