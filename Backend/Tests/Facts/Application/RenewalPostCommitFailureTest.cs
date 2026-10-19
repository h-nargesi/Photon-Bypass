using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Billing;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.Application;

/// <summary>
/// Post-commit resilience of the renewal executor (docs/14 H24): once the
/// wallet debit, the renewal row and the radius sync are committed, a failure
/// in the follow-up steps (connection close / history / capacity check) must
/// not surface — the settlement caller would revert the invoice to pending
/// although the plan is active and the money is gone.
/// </summary>
public class RenewalPostCommitFailureTest : UnitLevelServiceInitializer
{
    private readonly Mock<ISessionRadiusSyncService> session_moq = CreateSessionMoq();

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddLazyTransient(provider => session_moq.Object);
    }

    [Fact]
    public async Task SettleWallet_CloseConnectionFailureAfterCommit_StillSucceeds()
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

        // "0u" < the seeded SimultaneousUser of 1 forces the post-commit
        // close-connections branch to run for this renewal
        var code = (await billing_app.IssuePlanInvoice(160, "User2t|0u|120d|50g", "پلن تست")).Data!.Value;

        var result = await billing_app.SettleWallet(code);

        // the committed settlement is a success, not a reverted invoice
        Assert.Equal(2, result.Code / 100);

        Assert.Equal(BalanceStatus.Canceled, invoice_moq.Data.Single(i => i.Code == code).Status);

        var debit = (await wallet_repo.GetTransactions(2)).Single(w => w.InvoiceCode == code && w.Direction == BalanceDirection.Debit);

        Assert.Equal(BalanceStatus.Completed, debit.Status);

        Assert.Contains(renewal_moq.Data, r => r.WalletDebit == debit.Id);

        Assert.Equal(renewals_before + 1, renewal_moq.Data.Count);

        session_moq.Verify(
            x => x.CloseConnectionByUsername(It.IsAny<int?>(), It.Is<string>(user => user == "User2")),
            Times.Once);
    }

    private static Mock<ISessionRadiusSyncService> CreateSessionMoq()
    {
        var moq = new Mock<ISessionRadiusSyncService>();

        moq.Setup(x => x.CloseConnectionByUsername(It.IsAny<int?>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("ssh down after commit"));

        return moq;
    }
}
