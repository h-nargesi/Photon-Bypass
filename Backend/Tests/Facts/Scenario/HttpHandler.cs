using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Facts.Scenario;

public partial class HttpHandler(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
{
    protected async Task Register(string username, string phone, 
        string password = "Password", string firstname = "fname", string lastname = "lname", int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterModel
        {
            Firstname = firstname,
            Lastname = lastname,
            Mobile = $"+9891212345{phone}",
            Email = $"{username}@gmail.com",
            Username = username,
            Password = password,
        });
        await CheckResponse(response, status);
    }

    protected async Task Login(string username, string password, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = username,
            Password = password,
        });
        SetToken(await CheckResponseObject(response, status));
    }

    protected async Task<Dictionary<string, string?>> GetUser(int status = 2)
    {
        var response = await Client.GetAsync("/api/account/get-user");
        return await CheckResponseObject(response, status);
    }

    protected async Task<Dictionary<string, string?>> FullInfo(int status = 2)
    {
        var response = await Client.GetAsync("/api/account/full-info");
        return await CheckResponseObject(response, status);
    }

    protected async Task<Dictionary<string, string?>> FullInfo(string taget, int status = 2)
    {
        var response = await Client.GetAsync($"/api/account/full-info?taget={taget}");
        return await CheckResponseObject(response, status);
    }

    protected async Task EditUser(string username, Dictionary<string, string?> user, int status = 2)
    {
        // /api/account/edit-user
        user["firstname"] = "first-name";
        user["lastname"] = "last-name";
        var response = await Client.PostAsJsonAsync($"/api/account/edit-user?target={username}", new EditUserModel
        {
            Email = user["email"],
            Firstname = user["firstname"],
            Lastname = user["lastname"],
            Mobile = user["mobile"],
        });
        await CheckResponse(response, status);
    }

    protected async Task ChangePass(string token, string password, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/account/change-pass", new ChangePasswordContext
        {
            Token = token,
            Password = password,
        });
        await CheckResponse(response, status);
    }

    protected async Task<string> ForgetPass(string username, int status = 2)
    {
       var code = string.Empty;
        var email_srv = ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        email_srv.OnSend += message =>
        {
            var match = CodeSelector().Match(message.Body);
            Assert.True(match.Success);
            code = match.Groups[1].Value;
        };

        var response = await Client.PostAsJsonAsync("/api/auth/forget-pass", new ResetPasswordContext
        {
            EmailMobile = $"{username}@gmail.com",
        });
        await CheckResponse(response, status);

        return code;
    }

    protected async Task ResetPass(string code, string password, int status = 2)
    {
       var response = await Client.PostAsJsonAsync("/api/auth/reset-pass", new ChangePasswordContext
       {
           Token = code,
           Password = password,
       });
       await CheckResponse(response, status);
    }

    protected async Task<Dictionary<string, string?>[]> Prices(int status = 2)
    {
       var response = await Client.GetAsync("/api/basics/prices");
       return await CheckResponseArray(response, status);
    }

    protected async Task<Dictionary<string, string?>> PlanState(int status = 2)
    {
       var response = await Client.GetAsync("/api/plan/plan-state");
       return await CheckResponseObject(response, status);
    }

    protected async Task<Dictionary<string, string?>> PlanInfo(int status = 2)
    {
       var response = await Client.GetAsync("/api/plan/plan-info");
       return await CheckResponseObject(response, status);
    }

    protected async Task<Dictionary<string, string?>> Estimate(string username, byte users, short? days, short? gigs, int status = 2)
    {
       var request = new RenewalContext
       {
           Target = username,
           SimultaneousUserCount = users,
           Days = days,
           Gigabytes = gigs,
       };
       var response = await Client.PostAsJsonAsync("/api/plan/estimate", request);
       return await CheckResponseObject(response, status);
    }

    protected async Task<Dictionary<string, string?>> Renewal(string username, byte users, short? days, short? gigs, int status = 2)
    {
       var request = new RenewalContext
       {
           Target = username,
           SimultaneousUserCount = users,
           Days = days,
           Gigabytes = gigs,
       };
       var response = await Client.PostAsJsonAsync("/api/plan/renewal", request);
       return await CheckResponseObject(response, status);
    }

    protected async Task Estimate(string username, string token, string password, int status = 2)
    {
       var response = await Client.PostAsJsonAsync("/api/vpn/change-ovpn", new ChangeOvpnContext
       {
           Target = username,
           Token = token,
           Password = password,
       });
       await CheckResponseObject(response, status);
    }

    protected async Task ChangeOVpn(string username, string token, string password, int status = 2)
    {
        var request = new ChangeOvpnContext
        {
            Target = username,
            Token = token,
            Password = password,
        };
        var response = await Client.PostAsJsonAsync("/api/vpn/change-ovpn", request);
        await CheckResponseObject(response, status);
    }

    protected async Task FakeAddMoney(string username, int value)
    {
        using var scope = ServiceProvider.CreateScope();

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        var account = await account_repo.GetAccount(username);
        Assert.NotNull(account);

        await wallet_repo.Save(new WalletEntity
        {
            AccountId = account.Id,
            Amount = value,
            Direction = BalanceDirection.Credit,
            Status = BalanceStatus.Completed,
            Description = "Fake Add Money",
        });
    }

    [GeneratedRegex(@"<div class=""code-box"">(\w+)</div>")]
    private static partial Regex CodeSelector();
}