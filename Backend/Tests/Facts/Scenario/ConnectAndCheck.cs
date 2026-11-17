using System.Net.Http.Json;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class ConnectAndCheck(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Test()
    {
        // /api/auth/register
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterModel
        {
            Firstname = "fname",
            Lastname = "lname",
            Mobile = "+989121234568",
            Email = "user02@gmail.com",
            Username = "user02",
            Password = "Passw0rd1",
        });
        await CheckResponse(response);

        // /api/auth/token
        response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = "user02",
            Password = "Passw0rd1",
        });
        SetToken(await CheckResponseObject(response));

        // /api/account/full-info
        response = await Client.GetAsync("/api/account/full-info");
        var user = await CheckResponseObject(response);

        Assert.NotNull(user);
        Assert.Equal("user02", user["username"].ToString());

        response = await Client.GetAsync("/api/account/full-info");
        var edition_2 = await CheckResponseObject(response);

        Assert.NotNull(edition_2);
        foreach (var prop in edition_2)
        {
            Assert.Equal(user[prop.Key].ToString(), prop.Value.ToString());
        }

        // /api/plan/new-account — the first purchase creates the VPN account
        // (Docs/22 D9); realm 1 is the scenario realm
        var username = user["username"].ToString()!;

        response = await Client.PostAsJsonAsync("/api/plan/new-account", new NewAccountContext
        {
            RealmId = 1,
            SimultaneousUserCount = 1,
            Days = 120,
            Gigabytes = 50,
        });
        var purchase = await CheckResponseObject(response);

        Assert.NotNull(purchase["invoiceCode"]);

        // /api/billing/get-invoice — kind only; allowWallet needs the balance
        response = await Client.GetAsync($"/api/billing/get-invoice?code={purchase["invoiceCode"]}");
        var invoice = await CheckResponseObject(response);

        Assert.NotNull(invoice);
        Assert.Equal("2", invoice["kind"].ToString());

        // add money
        await FakeAddMoney(username, int.Parse(invoice["totalPrice"].ToString()!));

        response = await Client.GetAsync($"/api/billing/get-invoice?code={purchase["invoiceCode"]}");
        invoice = await CheckResponseObject(response);
        Assert.Equal("True", invoice["allowWallet"].ToString());

        // /api/billing/settle-wallet
        response = await Client.PostAsJsonAsync("/api/billing/settle-wallet",
            new { code = int.Parse(purchase["invoiceCode"]!) });
        await CheckResponse(response);

        // /api/vpn/accounts — the purchased account
        var account_id = (await VpnAccounts()).Single()["id"];

        // /api/plan/plan-info
        response = await Client.GetAsync($"/api/plan/plan-info?accountId={account_id}");
        var plan_info = await CheckResponseObject(response);

        Assert.NotNull(plan_info);
        Assert.Equal("120", plan_info["days"].ToString());
        Assert.Equal("50", plan_info["gigabytes"].ToString());
        Assert.Equal("1", plan_info["simultaneousUserCount"].ToString());
        Assert.Equal(username, plan_info["target"].ToString());

        // /api/plan/plan-info
    }
}
