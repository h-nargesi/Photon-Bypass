using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Billing;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.Application;

public class SettlementRollbackTest : UnitLevelServiceInitializer
{
    private static bool deactivated;

    private readonly Mock<IAccountRadiusSyncService> radius_moq = CreateRadiusMoq();

    public SettlementRollbackTest()
    {
        deactivated = false;
    }

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddLazyTransient(provider => radius_moq.Object);
    }

    [Fact]
    public async Task SettleWallet_RadiusSyncFailureInsideTransaction_RollsBackDebitAndRenewal()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        job_context.InjectJobContext(2, "User2", "User2");

        var renewals_before = renewal_moq.Data.Count;

        var code = (await billing_app.IssuePlanInvoice(230, "User2t|2u|120d|75g", "پلن تست")).Data!.Value;

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(code));

        Assert.Equal(BalanceStatus.Pending, invoice_moq.Data.Single(i => i.Code == code).Status);

        Assert.Empty(wallet_moq.Data[2].Where(w => w.Direction == BalanceDirection.Debit));

        Assert.Equal(renewals_before, renewal_moq.Data.Count);

        Assert.Equal(1500, await wallet_repo.GetBalance(2));

        Assert.True(deactivated);
    }

    private static Mock<IAccountRadiusSyncService> CreateRadiusMoq()
    {
        var moq = new Mock<IAccountRadiusSyncService>();

        moq.Setup(x => x.SyncUserAndActive(It.IsAny<AccountEntity>(), It.IsAny<RenewalEntity>()))
            .ThrowsAsync(new Exception("radius sync down"));

        moq.Setup(x => x.DeactivateUsers(It.IsAny<IEnumerable<string>>()))
            .Callback(() => deactivated = true)
            .Returns(Task.CompletedTask);

        return moq;
    }
}

public class SettlementRollbackCompensationFailureTest : UnitLevelServiceInitializer
{
    private readonly Mock<IAccountRadiusSyncService> radius_moq = CreateRadiusMoq();

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddLazyTransient(provider => radius_moq.Object);
    }

    [Fact]
    public async Task SettleWallet_RadiusSyncAndCompensationFailure_StillRollsBackAndSurfacesError()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(2, "User2", "User2");

        var code = (await billing_app.IssuePlanInvoice(230, "User2t|2u|120d|75g", "پلن تست")).Data!.Value;

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(code));

        Assert.Equal(BalanceStatus.Pending, invoice_moq.Data.Single(i => i.Code == code).Status);

        Assert.Empty(wallet_moq.Data[2].Where(w => w.Direction == BalanceDirection.Debit));

        Assert.Equal(1500, await wallet_repo.GetBalance(2));
    }

    private static Mock<IAccountRadiusSyncService> CreateRadiusMoq()
    {
        var moq = new Mock<IAccountRadiusSyncService>();

        moq.Setup(x => x.SyncUserAndActive(It.IsAny<AccountEntity>(), It.IsAny<RenewalEntity>()))
            .ThrowsAsync(new Exception("radius sync down"));

        moq.Setup(x => x.DeactivateUsers(It.IsAny<IEnumerable<string>>()))
            .ThrowsAsync(new Exception("deactivate also down"));

        return moq;
    }
}
