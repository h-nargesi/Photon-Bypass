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
        var password = "Password";
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
        password = "new-password";
        await ChangePass("Password", password);
        await Login(username, password);

        // reset-password
        var code = await ForgetPass(username);
        password = "reset-password";
        await ResetPass(code, password);
        await Login(username, password);

        // /api/basics/prices
        var prices = await Prices();
        Assert.NotNull(prices);
        Assert.Single(prices);
        Assert.Equal(3, prices[0].Count);

        // /api/plan/plan-state
        var plan_state = await PlanState();
        Assert.NotNull(plan_state);
        Assert.Null(plan_state["remainsTitle"]);
        Assert.Null(plan_state["remainsTrafficPercent"]);
        Assert.Null(plan_state["remainsTimePercent"]);
        Assert.Equal("0", plan_state["simultaneousUserCount"]);

        // /api/plan/plan-info
        var plan_info = await PlanInfo();
        Assert.NotNull(plan_info);
        Assert.Null(plan_info["days"]);
        Assert.Null(plan_info["gigabytes"]);
        Assert.Equal("0", plan_info["simultaneousUserCount"]);
        Assert.Equal(username, plan_info["target"]);

        // /api/plan/estimate
        var estimate = await Estimate(username, 1, 120, 50);
        Assert.NotNull(estimate);
        Assert.NotNull(estimate["price"]);
        Assert.NotNull(estimate["days"]);
        Assert.NotNull(estimate["gigabytes"]);
        Assert.NotNull(estimate["simultaneousUserCount"]);

        // add money
        await FakeAddMoney(username, int.Parse(estimate["price"]!));

        // /api/plan/renewal
        var renewal = await Renewal(username, 1, 120, 50);
        Assert.NotNull(renewal);
        Assert.Equal("0", renewal["currentPrice"]);
        Assert.Null(renewal["invoiceCode"]);

        // /api/plan/plan-info
        plan_info = await PlanInfo();
        Assert.NotNull(plan_info);
        Assert.Equal(estimate["days"], plan_info["days"]);
        Assert.Equal(estimate["gigabytes"], plan_info["gigabytes"]);
        Assert.Equal(estimate["simultaneousUserCount"], plan_info["simultaneousUserCount"]);
        Assert.Equal(username, plan_info["target"]);

        // /api/vpn/change-ovpn
        await ChangeOVpn(username, password, "change-ovp-password");

        // /api/account/history
    }
}