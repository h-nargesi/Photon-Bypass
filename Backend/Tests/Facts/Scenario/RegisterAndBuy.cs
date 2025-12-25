using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.Scenario;

public partial class RegisterAndBuy(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
{
    [Fact]
    public async Task Register()
    {
        // /api/auth/register
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterModel
        {
            Firstname = "fname",
            Lastname = "lname",
            Mobile = "+989121234567",
            Email = "ryan@gmail.com",
            Username = "user01",
            Password = "Password",
        });
        await CheckResponse(response);

        // /api/auth/token
        response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = "user01",
            Password = "Password",
        });
        SetToken(await CheckResponseObject(response));

        // /api/account/get-user
        response = await Client.GetAsync("/api/account/get-user");
        var user = (await CheckResponseObject(response)).Data;

        Assert.NotNull(user);
        Assert.Equal("fname lname", user["fullname"].ToString());

        // /api/account/full-info
        response = await Client.GetAsync("/api/account/full-info");
        user = (await CheckResponseObject(response)).Data;

        Assert.NotNull(user);
        Assert.Equal("user01", user["username"].ToString());

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
        var edition_2 = (await CheckResponseObject(response)).Data;

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
            Username = "user01",
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
            EmailMobile = "ryan@gmail.com",
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
            Username = "user01",
            Password = "reset-password",
        });
        SetToken(await CheckResponseObject(response));

        // /api/basics/prices
        response = await Client.GetAsync("/api/basics/prices");
        var prices = (await CheckResponseArray(response)).Data;

        Assert.NotNull(prices);
        Assert.Single(prices);
        Assert.Equal(3, prices[0].Count);
        
        // /api/plan/plan-info
        response = await Client.GetAsync("/api/plan/plan-info");
        var plan_info = (await CheckResponseObject(response)).Data;
        
        Assert.NotNull(plan_info);
    }

    [GeneratedRegex(@"<div class=""code-box"">(\w+)</div>")]
    private static partial Regex CodeSelector();
}