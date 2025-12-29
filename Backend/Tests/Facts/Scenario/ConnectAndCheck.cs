using System.Net.Http.Json;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class ConnectAndCheck(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
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
            Password = "Password",
        });
        await CheckResponse(response);

        // /api/auth/token
        response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = "user02",
            Password = "Password",
        });
        SetToken(await CheckResponseObject(response));

        // /api/account/full-info
        response = await Client.GetAsync("/api/account/full-info");
        var user = (await CheckResponseObject(response)).Data;

        Assert.NotNull(user);
        Assert.Equal("user02", user["username"].ToString());

        response = await Client.GetAsync("/api/account/full-info");
        var edition_2 = (await CheckResponseObject(response)).Data;

        Assert.NotNull(edition_2);
        foreach (var prop in edition_2)
        {
            Assert.Equal(user[prop.Key].ToString(), prop.Value.ToString());
        }

        // /api/plan/estimate
        var request = new RenewalContext
        {
            Target = user["username"].ToString(),
            SimultaneousUserCount = 1,
            Days = 120,
            Gigabytes = 50,
        };
        response = await Client.PostAsJsonAsync("/api/plan/estimate", request);
        var estimate = (await CheckResponseObject(response)).Data;

        Assert.NotNull(estimate);
        Assert.NotNull(estimate["price"]);
        Assert.NotNull(estimate["days"]);
        Assert.NotNull(estimate["gigabytes"]);
        Assert.NotNull(estimate["simultaneousUserCount"]);

        // add money
        await FakeAddMoney(user["username"].ToString()!, int.Parse(estimate["price"].ToString()!));

        // /api/plan/renewal
        response = await Client.PostAsJsonAsync("/api/plan/renewal", request);
        var renewal = (await CheckResponseObject(response)).Data;

        Assert.NotNull(renewal);
        Assert.Equal("0", renewal["currentPrice"].ToString());
        Assert.Equal("0", renewal["moneyNeeds"].ToString());

        // /api/plan/plan-info
        response = await Client.GetAsync("/api/plan/plan-info");
        var plan_info = (await CheckResponseObject(response)).Data;

        Assert.NotNull(plan_info);
        Assert.Equal(estimate["days"].ToString(), plan_info["days"].ToString());
        Assert.Equal(estimate["gigabytes"].ToString(), plan_info["gigabytes"].ToString());
        Assert.Equal(estimate["simultaneousUserCount"].ToString(), plan_info["simultaneousUserCount"].ToString());
        Assert.Equal(user["username"].ToString(), plan_info["target"].ToString());

        // /api/plan/plan-info
    }
}