using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

/// <summary>
/// Payer separation in the money path (Docs/20 phase 2, D4/D6): the invoice,
/// wallet credit/debit and balance always belong to the paying actor, the
/// renewed plan belongs to the target, and a dependent account (!AllowSelfService)
/// is refused its own financial endpoints with a user-friendly 403.
/// </summary>
public class PayerSeparationTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task DependentUser_AllSelfFinancialEndpoints_Forbidden()
    {
        var username = "dep_self01";

        int account_id;

        await InsertAccount(username, UserTypes.None);

        // the dependent owns a VPN account (Docs/22 D9): the refusals below
        // come from the D6 self-service guard, not from a missing account
        using (var scope = ServiceProvider.CreateScope())
        {
            var panel_user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
            var vpn_account_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();

            var user_id = await panel_user_repo.GetActiveUserId(username);

            await vpn_account_repo.Save(new VpnAccountEntity
            {
                UserId = user_id!.Value,
                RealmId = 1,
                Username = username,
                VpnPassword = "VpnPassword",
            });

            account_id = (await vpn_account_repo.GetAccount(username))!.Id;
        }

        await Login(username, "Passw0rd1");

        await Renewal(account_id, 1, 120, 50, status: 403);
        await Estimate(account_id, 1, 120, 50, status: 403);
        await Pay(50_000, status: 403);
        await GetInvoice(999_999, status: 403);
        await RegisterReceipt(999_999, text: "receipt", status: 403);
        await SettleWallet(999_999, status: 403);
    }

    [Fact]
    public async Task AdminPaysOnBehalf_InvoiceDebitOnAdmin_PlanOnTarget()
    {
        var target = "dep_target02";

        int account_id;

        await InsertAccount(target, UserTypes.None);

        // Docs/22 D9: the first purchase is always actor-scoped, so the
        // target's account is seeded directly (realm 1 = the scenario realm)
        using (var scope = ServiceProvider.CreateScope())
        {
            var panel_user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
            var vpn_account_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();

            var target_id = await panel_user_repo.GetActiveUserId(target);

            await vpn_account_repo.Save(new VpnAccountEntity
            {
                UserId = target_id!.Value,
                RealmId = 1,
                Username = target,
                VpnPassword = "VpnPassword",
            });

            account_id = (await vpn_account_repo.GetAccount(target))!.Id;
        }

        await Login("admin", "admin");

        // on-behalf estimate is allowed (D6 gates only the self view)
        var estimate = await Estimate(account_id, 1, 120, 50);
        var price = int.Parse(estimate["price"]!);

        await FakeAddMoney("admin", price);

        var renewal = await Renewal(account_id, 1, 120, 50);
        var code = int.Parse(renewal["invoiceCode"]!);

        // the payer's invoice opens in the admin's own view (no target)
        var invoice = await GetInvoice(code);
        Assert.Equal("True", invoice["allowWallet"]);
        Assert.Equal(price.ToString(), invoice["totalPrice"]);

        await SettleWallet(code);

        using (var scope = ServiceProvider.CreateScope())
        {
            var panel_user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
            var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

            var admin_id = await panel_user_repo.GetActiveUserId("admin");
            var target_id = await panel_user_repo.GetActiveUserId(target);

            Assert.Equal(0, await wallet_repo.GetBalance(admin_id!.Value));
            Assert.DoesNotContain(await wallet_repo.GetTransactions(target_id!.Value),
                w => w.Direction == BalanceDirection.Debit);
        }

        // the renewed plan is applied to the target's VPN account
        var plan_info = await PlanInfo(account_id);
        Assert.Equal(estimate["days"], plan_info["days"]);
        Assert.Equal(estimate["gigabytes"], plan_info["gigabytes"]);
        Assert.Equal(estimate["simultaneousUserCount"], plan_info["simultaneousUserCount"]);
    }

    [Fact]
    public async Task Pay_IssuesTopUpInvoiceForActor()
    {
        await Register("dep_payer03", "82", "Passw0rd1");
        await Login("dep_payer03", "Passw0rd1");

        var code = await Pay(50_000);

        Assert.NotNull(code);

        var invoice = await GetInvoice(code!.Value);
        Assert.Equal(((int)InvoiceKind.TopUp).ToString(), invoice["kind"]);
        Assert.Equal("50000", invoice["totalPrice"]);
        Assert.Equal("50000", invoice["payable"]);
    }
}
