using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Facts.Scenario;

public partial class HttpHandler(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
{
    protected async Task Register(string username, string phone)
    {
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
    }

    protected async Task Login(string username, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = username,
            Password = password,
        });
        SetToken(await CheckResponseObject(response));
    }

    protected async Task<Dictionary<string, string>> GetUser()
    {
        var response = await Client.GetAsync("/api/account/get-user");
        return await CheckResponseObject(response);
    }

    protected async Task<Dictionary<string, string>> FullInfo()
    {
        var response = await Client.GetAsync("/api/account/full-info");
        return await CheckResponseObject(response);
    }

    protected async Task<Dictionary<string, string>> FullInfo(string taget, int status = 2)
    {
        var response = await Client.GetAsync($"/api/account/full-info?taget={taget}");
        return await CheckResponseObject(response, status);
    }

    protected async Task EditUser(string username, Dictionary<string, string> user)
    {
        // /api/account/edit-user
        user["firstname"] = "first-name";
        user["lastname"] = "last-name";
        var response = await Client.PostAsJsonAsync($"/api/account/edit-user?target={username}", new EditUserModel
        {
            Email = user["email"].ToString(),
            Firstname = user["firstname"].ToString(),
            Lastname = user["lastname"].ToString(),
            Mobile = user["mobile"].ToString(),
        });
        await CheckResponse(response);
    }

    protected async Task ChangePass(string token, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/account/change-pass", new ChangePasswordContext
        {
            Token = token,
            Password = password,
        });
        await CheckResponse(response);
    }

    protected async Task ResetPass(string token, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/account/change-pass", new ChangePasswordContext
        {
            Token = token,
            Password = password,
        });
        await CheckResponse(response);
    }

    //protected async Task OnePerson(string username, EditUserModel model)
    //{
    //    // reset-password
    //    var code = string.Empty;
    //    var email_srv = ServiceProvider.GetRequiredService<EmailHandlerMoq>();
    //    email_srv.OnSend += message =>
    //    {
    //        var match = CodeSelector().Match(message.Body);
    //        code = match.Groups[1].Value;
    //    };
    //    response = await Client.PostAsJsonAsync("/api/auth/forget-pass", new ResetPasswordContext
    //    {
    //        EmailMobile = $"{username}@gmail.com",
    //    });
    //    await CheckResponse(response);

    //    response = await Client.PostAsJsonAsync("/api/auth/reset-pass", new ChangePasswordContext
    //    {
    //        Token = code,
    //        Password = "reset-password",
    //    });
    //    await CheckResponse(response);

    //    response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
    //    {
    //        Username = username,
    //        Password = "reset-password",
    //    });
    //    SetTokenawait CheckResponseObject(response);

    //    // /api/basics/prices
    //    response = await Client.GetAsync("/api/basics/prices");
    //    var prices = (await CheckResponseArray(response));

    //    Assert.NotNull(prices);
    //    Assert.Single(prices);
    //    Assert.Equal(3, prices[0].Count);

    //    // /api/plan/plan-state
    //    response = await Client.GetAsync("/api/plan/plan-state");
    //    var plan_state = await CheckResponseObject(response);

    //    Assert.NotNull(plan_state);
    //    Assert.Null(plan_state["remainsTitle"]);
    //    Assert.Null(plan_state["remainsTrafficPercent"]);
    //    Assert.Null(plan_state["remainsTimePercent"]);
    //    Assert.Equal("0", plan_state["simultaneousUserCount"].ToString());

    //    // /api/plan/plan-info
    //    response = await Client.GetAsync("/api/plan/plan-info");
    //    var plan_info = await CheckResponseObject(response);

    //    Assert.NotNull(plan_info);
    //    Assert.Null(plan_info["days"]);
    //    Assert.Null(plan_info["gigabytes"]);
    //    Assert.Equal("0", plan_info["simultaneousUserCount"].ToString());
    //    Assert.Equal(user["username"].ToString(), plan_info["target"].ToString());

    //    // /api/plan/estimate
    //    var request = new RenewalContext
    //    {
    //        Target = user["username"].ToString(),
    //        SimultaneousUserCount = 1,
    //        Days = 120,
    //        Gigabytes = 50,
    //    };
    //    response = await Client.PostAsJsonAsync("/api/plan/estimate", request);
    //    var estimate = await CheckResponseObject(response);

    //    Assert.NotNull(estimate);
    //    Assert.NotNull(estimate["price"]);
    //    Assert.NotNull(estimate["days"]);
    //    Assert.NotNull(estimate["gigabytes"]);
    //    Assert.NotNull(estimate["simultaneousUserCount"]);

    //    // add money
    //    await FakeAddMoney(user["username"].ToString()!, int.Parse(estimate["price"].ToString()!));

    //    // /api/plan/renewal
    //    response = await Client.PostAsJsonAsync("/api/plan/renewal", request);
    //    var renewal = await CheckResponseObject(response);

    //    Assert.NotNull(renewal);
    //    Assert.Equal("0", renewal["currentPrice"].ToString());
    //    Assert.Null(renewal["invoiceCode"]);

    //    // /api/plan/plan-info
    //    response = await Client.GetAsync("/api/plan/plan-info");
    //    plan_info = await CheckResponseObject(response);

    //    Assert.NotNull(plan_info);
    //    Assert.Equal(estimate["days"].ToString(), plan_info["days"].ToString());
    //    Assert.Equal(estimate["gigabytes"].ToString(), plan_info["gigabytes"].ToString());
    //    Assert.Equal(estimate["simultaneousUserCount"].ToString(), plan_info["simultaneousUserCount"].ToString());
    //    Assert.Equal(user["username"].ToString(), plan_info["target"].ToString());

    //    // /api/vpn/change-ovpn
    //    response = await Client.PostAsJsonAsync("/api/vpn/change-ovpn", new ChangeOvpnContext
    //    {
    //        Target = user["username"].ToString(),
    //        Token = "reset-password",
    //        Password = "change-ovp-password",
    //    });
    //    await CheckResponseObject(response);
    //}

    [GeneratedRegex(@"<div class=""code-box"">(\w+)</div>")]
    private static partial Regex CodeSelector();
}