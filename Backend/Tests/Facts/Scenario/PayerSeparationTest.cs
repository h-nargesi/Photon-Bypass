using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

/// <summary>
/// Payer separation in the money path (docs/20 phase 2, D4/D6): the invoice,
/// wallet credit/debit and balance always belong to the paying actor, the
/// renewed plan belongs to the target, and a dependent account (!AllowSelfService)
/// is refused its own financial endpoints with a user-friendly 403.
/// </summary>
public class PayerSeparationTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task DependentUser_AllSelfFinancialEndpoints_Forbidden()
    {
        await InsertAccount("dep_self01", UserTypes.None);
        await Login("dep_self01", "Password");

        await Renewal("dep_self01", 1, 120, 50, status: 403);
        await Estimate("dep_self01", 1, 120, 50, status: 403);
        await Pay(50_000, status: 403);
        await GetInvoice(999_999, status: 403);
        await RegisterReceipt(999_999, text: "receipt", status: 403);
        await SettleWallet(999_999, status: 403);
    }

    [Fact]
    public async Task AdminPaysOnBehalf_InvoiceDebitOnAdmin_PlanOnTarget()
    {
        var target = "dep_target02";

        await InsertAccount(target, UserTypes.None);
        await Login("admin", "admin");

        // on-behalf estimate is allowed (D6 gates only the self view)
        var estimate = await Estimate(target, 1, 120, 50);
        var price = int.Parse(estimate["price"]!);

        await FakeAddMoney("admin", price);

        var renewal = await Renewal(target, 1, 120, 50);
        var code = int.Parse(renewal["invoiceCode"]!);

        // the payer's invoice opens in the admin's own view (no target)
        var invoice = await GetInvoice(code);
        Assert.Equal("True", invoice["allowWallet"]);
        Assert.Equal(price.ToString(), invoice["totalPrice"]);

        await SettleWallet(code);

        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

            var ids = await account_repo.GetAccountIdByUsername(["admin", target]);

            Assert.Equal(0, await wallet_repo.GetBalance(ids["admin"]));
            Assert.DoesNotContain(await wallet_repo.GetTransactions(ids[target]),
                w => w.Direction == BalanceDirection.Debit);
        }

        // the renewed plan is applied to the target
        var plan_info = await PlanInfo(target);
        Assert.Equal(estimate["days"], plan_info["days"]);
        Assert.Equal(estimate["gigabytes"], plan_info["gigabytes"]);
        Assert.Equal(estimate["simultaneousUserCount"], plan_info["simultaneousUserCount"]);
    }

    [Fact]
    public async Task Pay_IssuesTopUpInvoiceForActor()
    {
        await Register("dep_payer03", "82", "Password");
        await Login("dep_payer03", "Password");

        var code = await Pay(50_000);

        Assert.NotNull(code);

        var invoice = await GetInvoice(code!.Value);
        Assert.Equal(((int)InvoiceKind.TopUp).ToString(), invoice["kind"]);
        Assert.Equal("50000", invoice["totalPrice"]);
        Assert.Equal("50000", invoice["payable"]);
    }
}
