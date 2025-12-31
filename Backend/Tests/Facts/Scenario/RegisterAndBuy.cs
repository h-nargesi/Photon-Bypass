using System.Net.Http.Json;
using System.Text.RegularExpressions;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.Scenario;

public partial class RegisterAndBuy(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
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
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterModel
        {
            Firstname = "fname",
            Lastname = "lname",
            Mobile = $"+9891212345{phone}",
            Email = $"{username}@gmail.com",
            Username = username,
            Password = "Password",
        });
        await CheckResponse(response);

        // /api/auth/token
        response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = username,
            Password = "Password",
        });
        SetToken(await CheckResponseObject(response));

        // /api/account/get-user
        response = await Client.GetAsync("/api/account/get-user");
        var user = await CheckResponseObject(response);

        Assert.NotNull(user);
        Assert.Equal("fname lname", user["fullname"].ToString());

        // /api/account/full-info
        response = await Client.GetAsync("/api/account/full-info");
        user = await CheckResponseObject(response);

        Assert.NotNull(user);
        Assert.Equal(username, user["username"].ToString());

        // /api/account/edit-user
        user["firstname"] = "first-name";
        user["lastname"] = "last-name";
        response = await Client.PostAsJsonAsync($"/api/account/edit-user?target={user["username"]}", new EditUserModel
        {
            Email = user["email"].ToString(),
            Firstname = user["firstname"].ToString(),
            Lastname = user["lastname"].ToString(),
            Mobile = user["mobile"].ToString(),
        });
        await CheckResponse(response);

        response = await Client.GetAsync("/api/account/full-info");
        var edition_2 = await CheckResponseObject(response);

        Assert.NotNull(edition_2);
        foreach (var prop in edition_2)
        {
            Assert.Equal(user[prop.Key].ToString(), prop.Value.ToString());
        }

        // /api/account/change-pass
        response = await Client.PostAsJsonAsync("/api/account/change-pass", new ChangePasswordContext
        {
            Token = "Password",
            Password = "new-password",
        });
        await CheckResponse(response);

        response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = username,
            Password = "new-password",
        });
        SetToken(await CheckResponseObject(response));

        // reset-password
        var code = string.Empty;
        var email_srv = ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        email_srv.OnSend += message =>
        {
            var match = CodeSelector().Match(message.Body);
            code = match.Groups[1].Value;
        };
        response = await Client.PostAsJsonAsync("/api/auth/forget-pass", new ResetPasswordContext
        {
            EmailMobile = $"{username}@gmail.com",
        });
        await CheckResponse(response);

        response = await Client.PostAsJsonAsync("/api/auth/reset-pass", new ChangePasswordContext
        {
            Token = code,
            Password = "reset-password",
        });
        await CheckResponse(response);

        response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = username,
            Password = "reset-password",
        });
        SetToken(await CheckResponseObject(response));

        // /api/basics/prices
        response = await Client.GetAsync("/api/basics/prices");
        var prices = await CheckResponseArray(response);

        Assert.NotNull(prices);
        Assert.Single(prices);
        Assert.Equal(3, prices[0].Count);

        // /api/plan/plan-state
        response = await Client.GetAsync("/api/plan/plan-state");
        var plan_state = await CheckResponseObject(response);

        Assert.NotNull(plan_state);
        Assert.Null(plan_state["remainsTitle"]);
        Assert.Null(plan_state["remainsTrafficPercent"]);
        Assert.Null(plan_state["remainsTimePercent"]);
        Assert.Equal("0", plan_state["simultaneousUserCount"].ToString());

        // /api/plan/plan-info
        response = await Client.GetAsync("/api/plan/plan-info");
        var plan_info = await CheckResponseObject(response);

        Assert.NotNull(plan_info);
        Assert.Null(plan_info["days"]);
        Assert.Null(plan_info["gigabytes"]);
        Assert.Equal("0", plan_info["simultaneousUserCount"].ToString());
        Assert.Equal(user["username"].ToString(), plan_info["target"].ToString());

        // /api/plan/estimate
        var request = new RenewalContext
        {
            Target = user["username"].ToString(),
            SimultaneousUserCount = 1,
            Days = 120,
            Gigabytes = 50,
        };
        response = await Client.PostAsJsonAsync("/api/plan/estimate", request);
        var estimate = await CheckResponseObject(response);

        Assert.NotNull(estimate);
        Assert.NotNull(estimate["price"]);
        Assert.NotNull(estimate["days"]);
        Assert.NotNull(estimate["gigabytes"]);
        Assert.NotNull(estimate["simultaneousUserCount"]);

        // add money
        await FakeAddMoney(user["username"].ToString()!, int.Parse(estimate["price"].ToString()!));

        // /api/plan/renewal
        response = await Client.PostAsJsonAsync("/api/plan/renewal", request);
        var renewal = await CheckResponseObject(response);

        Assert.NotNull(renewal);
        Assert.Equal("0", renewal["currentPrice"].ToString());
        Assert.Null(renewal["invoiceCode"]);

        // /api/plan/plan-info
        response = await Client.GetAsync("/api/plan/plan-info");
        plan_info = await CheckResponseObject(response);

        Assert.NotNull(plan_info);
        Assert.Equal(estimate["days"].ToString(), plan_info["days"].ToString());
        Assert.Equal(estimate["gigabytes"].ToString(), plan_info["gigabytes"].ToString());
        Assert.Equal(estimate["simultaneousUserCount"].ToString(), plan_info["simultaneousUserCount"].ToString());
        Assert.Equal(user["username"].ToString(), plan_info["target"].ToString());

        // /api/vpn/change-ovpn
        response = await Client.PostAsJsonAsync("/api/vpn/change-ovpn", new ChangeOvpnContext
        {
            Target = user["username"].ToString(),
            Token = "reset-password",
            Password = "change-ovp-password",
        });
        await CheckResponseObject(response);
        
        // /api/account/history
    }

    [GeneratedRegex(@"<div class=""code-box"">(\w+)</div>")]
    private static partial Regex CodeSelector();
}