using PhotonBypass.Application.Authentication.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Result;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Tools;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhotonBypass.Test.Facts.Scenario;

public partial class HttpHandler(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
{
    protected async Task Register(string username, string phone, 
        string password = "Passw0rd1", string firstname = "fname", string lastname = "lname", int status = 2)
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
        var token = await CheckResponseObject(response, status);
        if (status == 2) SetToken(token);
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
        var response = await Client.GetAsync($"/api/account/full-info?target={taget}");
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
            var match = CodeSelector().Match(EmailBody(message));
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

    /// <summary>
    /// The delivered HTML lives on the message's alternate view, not in
    /// <see cref="System.Net.Mail.MailMessage.Body"/>.
    /// </summary>
    private static string EmailBody(System.Net.Mail.MailMessage message)
    {
        using var reader = new System.IO.StreamReader(
            message.AlternateViews[0].ContentStream, System.Text.Encoding.UTF8);

        return reader.ReadToEnd();
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

    protected async Task<Dictionary<string, string?>> PlanInfo(string target, int status = 2)
    {
       var response = await Client.GetAsync($"/api/plan/plan-info?target={target}");
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

    protected async Task<Dictionary<string, string?>> GetInvoice(int code, int status = 2)
    {
        var response = await Client.GetAsync($"/api/billing/get-invoice?code={code}");
        return await CheckResponseObject(response, status);
    }

    protected async Task<int?> Pay(int value, string? target = null, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/billing/pay", new { value, target });
        var result = await CheckResponse<int?>(response, status);
        return result?.Data;
    }

    protected async Task<ApiResult> RegisterReceipt(
        int code,
        byte[]? file = null,
        string? file_name = null,
        string? content_type = null,
        string? text = null,
        string? target = null,
        int status = 2)
    {
        using var form = new MultipartFormDataContent();

        form.Add(new StringContent(code.ToString()), "code");

        if (target != null) form.Add(new StringContent(target), "target");
        if (text != null) form.Add(new StringContent(text), "text");

        if (file is { Length: > 0 })
        {
            var file_content = new ByteArrayContent(file);
            file_content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                content_type ?? "application/octet-stream");
            form.Add(file_content, "file", file_name ?? "receipt.jpg");
        }

        var response = await Client.PostAsync("/api/billing/register-receipt", form);
        return (await CheckResponse<object>(response, status))!;
    }

    protected async Task SettleWallet(int code, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/billing/settle-wallet", new { code });
        await CheckResponse(response, status);
    }

    protected async Task WalletAdjust(string? target, short direction, int amount, string? description, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/wallet/adjust",
            new { target, direction, amount, description });
        await CheckResponse(response, status);
    }

    protected async Task<Dictionary<string, string?>> WalletHistory(string? target = null, int page = 1, int status = 2)
    {
        var url = $"/api/wallet/history?page={page}" + (target != null ? $"&target={target}" : "");
        var response = await Client.GetAsync(url);
        return await CheckResponseObject(response, status);
    }

    protected async Task<Dictionary<string, string?>> UserList(string? search = null, int page = 1, int status = 2)
    {
        var url = $"/api/user/list?page={page}" +
                  (search != null ? $"&search={Uri.EscapeDataString(search)}" : "");
        var response = await Client.GetAsync(url);
        return await CheckResponseObject(response, status);
    }

    protected async Task UserCreate(string username, string password, bool self_service = false,
        int? role = null, string? owner = null, string? email = null, string? mobile = null,
        string firstname = "fname", string lastname = "lname", int status = 2)
    {
        var seed = Math.Abs(username.Aggregate(17, (current, c) => current * 31 + c));

        var response = await Client.PostAsJsonAsync("/api/user/create", new
        {
            username,
            password,
            firstname,
            lastname,
            email = email ?? $"{username}@gmail.com",
            mobile = mobile ?? $"+9893{seed % 100_000_000:D8}",
            allowSelfService = self_service,
            role,
            owner,
        });
        await CheckResponse(response, status);
    }

    protected async Task UserSetPassword(string target, string password, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/user/set-password", new { target, password });
        await CheckResponse(response, status);
    }

    protected async Task UserSetRole(string target, int role, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/user/set-role", new { target, role });
        await CheckResponse(response, status);
    }

    protected async Task UserSetOwner(string target, string? owner, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/user/set-owner", new { target, owner });
        await CheckResponse(response, status);
    }

    protected async Task UserSetSelfService(string target, bool allow, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/user/set-self-service", new { target, allow });
        await CheckResponse(response, status);
    }

    protected async Task UserSetActive(string target, bool active, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/user/set-active", new { target, active });
        await CheckResponse(response, status);
    }

    protected async Task<Dictionary<string, string?>[]> ServerList(int status = 2)
    {
        var response = await Client.GetAsync("/api/server/list");
        return await CheckResponseArray(response, status);
    }

    protected async Task ServerCreate(object payload, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/server/create", payload);
        await CheckResponse(response, status);
    }

    protected async Task ServerUpdate(object payload, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/server/update", payload);
        await CheckResponse(response, status);
    }

    protected async Task ServerSetActive(int id, bool active, int status = 2)
    {
        var response = await Client.PostAsJsonAsync("/api/server/set-active", new { id, active });
        await CheckResponse(response, status);
    }

    protected static List<Dictionary<string, string?>> ParseItems(Dictionary<string, string?> page)
    {
        return JsonSerializer.Deserialize<JsonElement>(page["items"]!)
            .EnumerateArray()
            .Select(element => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.ToString()))
            .ToList();
    }

    /// <summary>
    /// Creates an account straight in the scenario database — register always
    /// sets AllowSelfService (D11), so dependent/manager fixtures must be seeded here.
    /// </summary>
    protected async Task InsertAccount(string username, UserTypes user_type,
        AccountRole role = AccountRole.User, int? owner_id = null, bool is_active = true)
    {
        using var scope = ServiceProvider.CreateScope();

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        // deterministic per-username mobile (a length-based one collides)
        var seed = Math.Abs(username.Aggregate(17, (current, c) => current * 31 + c));

        await account_repo.Save(new AccountEntity
        {
            Username = username,
            Password = PasswordHasher.Hash("Passw0rd1"),
            VpnPassword = HashHandler.GenerateHashCode(),
            Email = $"{username}@gmail.com",
            Mobile = $"+9891{seed % 100_000_000:D8}",
            UserType = user_type,
            Role = role,
            Owner = owner_id,
            IsActive = is_active,
        });
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

    protected async Task<HttpResponseMessage> DownloadConfig(string? target = null, int status = 2)
    {
        var url = "/api/vpn/download" + (target != null ? $"?target={target}" : "");
        var response = await Client.GetAsync(url);

        if (status != 2)
        {
            await CheckResponse(response, status);
            return response;
        }

        response.EnsureSuccessStatusCode();
        return response;
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