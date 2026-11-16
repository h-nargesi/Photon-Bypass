using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public partial class RegisterAndBuy(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Simple()
    {
        await OnePerson("user01", "11");
    }

    [Fact]
    public async Task Twice()
    {
        await OnePerson("user12", "12");
        await OnePerson("user13", "13");
    }

    private async Task OnePerson(string username, string phone)
    {
        // /api/auth/register
        var password = "Passw0rd1";
        await Register(username, phone, password);

        // /api/auth/token
        await Login(username, password);

        // /api/account/get-user
        var user = await GetUser();
        Assert.NotNull(user);
        Assert.Equal("fname lname", user["fullname"]);

        // /api/account/full-info
        user = await FullInfo();
        Assert.NotNull(user);
        Assert.Equal(username, user["username"]);

        // /api/account/edit-user
        user["firstname"] = "first-name";
        user["lastname"] = "last-name";
        await EditUser(username, user);

        var edition_2 = await FullInfo();
        Assert.NotNull(edition_2);
        foreach (var prop in edition_2)
        {
            Assert.Equal(user[prop.Key], prop.Value);
        }

        // /api/account/change-pass
        password = "NewPassw0rd";
        await ChangePass("Passw0rd1", password);
        await Login(username, password);

        // reset-password
        var code = await ForgetPass(username);
        password = "ResetPassw0rd";
        await ResetPass(code, password);
        await Login(username, password);

        // /api/basics/prices
        var prices = await Prices();
        Assert.NotNull(prices);
        Assert.Single(prices);
        Assert.Equal(3, prices[0].Count);

        // /api/vpn/accounts — a fresh user owns no VPN account yet (docs/22 D9)
        Assert.Empty(await VpnAccounts());

        // /api/plan/new-account — the first purchase creates the VPN account
        var realm_id = int.Parse((await Realms()).Single(realm => realm["isSelectable"] == "True")["id"]!);

        var purchase = await NewAccount(null, realm_id, 1, 120, 50);
        Assert.NotNull(purchase["invoiceCode"]);

        // /api/billing/get-invoice — before the top-up only the kind is
        // asserted; allowWallet needs wallet_balance >= totalPrice
        var invoice = await GetInvoice(int.Parse(purchase["invoiceCode"]!));
        Assert.NotNull(invoice);
        Assert.Equal("2", invoice["kind"]);

        // add money
        await FakeAddMoney(username, int.Parse(invoice["totalPrice"]!));

        // /api/billing/get-invoice — with the balance in place the wallet
        // settlement becomes available
        invoice = await GetInvoice(int.Parse(purchase["invoiceCode"]!));
        Assert.Equal("True", invoice["allowWallet"]);

        // /api/billing/settle-wallet
        await SettleWallet(int.Parse(purchase["invoiceCode"]!));

        // the account exists now: D3 keeps the panel username for the first one
        var account_row = (await VpnAccounts()).Single();
        Assert.Equal(username, account_row["username"]);

        var account_id = int.Parse(account_row["id"]!);

        // /api/plan/plan-state
        var plan_state = await PlanState(account_id);
        Assert.NotNull(plan_state);
        Assert.Equal("1", plan_state["simultaneousUserCount"]);

        // /api/plan/plan-info
        var plan_info = await PlanInfo(account_id);
        Assert.NotNull(plan_info);
        Assert.Equal("120", plan_info["days"]);
        Assert.Equal("50", plan_info["gigabytes"]);
        Assert.Equal("1", plan_info["simultaneousUserCount"]);
        Assert.Equal(username, plan_info["target"]);

        // /api/plan/estimate — a renewal estimate for the existing account
        var estimate = await Estimate(account_id, 1, 120, 50);
        Assert.NotNull(estimate);
        Assert.NotNull(estimate["price"]);
        Assert.NotNull(estimate["days"]);
        Assert.NotNull(estimate["gigabytes"]);
        Assert.NotNull(estimate["simultaneousUserCount"]);

        // add money
        await FakeAddMoney(username, int.Parse(estimate["price"]!));

        // /api/plan/renewal — stacks a second plan on the same account
        var renewal = await Renewal(account_id, 1, 120, 50);
        Assert.NotNull(renewal);
        Assert.Equal(estimate["price"], renewal["currentPrice"]);
        Assert.NotNull(renewal["invoiceCode"]);

        // /api/billing/get-invoice
        invoice = await GetInvoice(int.Parse(renewal["invoiceCode"]!));
        Assert.NotNull(invoice);
        Assert.Equal("2", invoice["kind"]);
        Assert.Equal(estimate["price"], invoice["totalPrice"]);
        Assert.Equal("True", invoice["allowWallet"]);

        // /api/billing/settle-wallet
        await SettleWallet(int.Parse(renewal["invoiceCode"]!));

        // /api/plan/plan-info
        plan_info = await PlanInfo(account_id);
        Assert.NotNull(plan_info);
        Assert.Equal(estimate["days"], plan_info["days"]);
        Assert.Equal(estimate["gigabytes"], plan_info["gigabytes"]);
        Assert.Equal(estimate["simultaneousUserCount"], plan_info["simultaneousUserCount"]);
        Assert.Equal(username, plan_info["target"]);

        // /api/vpn/change-ovpn
        await ChangeOVpn(account_id, password, "change-ovp-password");

        // /api/account/history
    }
}
