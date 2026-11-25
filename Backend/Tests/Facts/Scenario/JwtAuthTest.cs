using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Result;
using PhotonBypass.Test.Initializer;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace PhotonBypass.Test.Facts.Scenario;

public class JwtAuthTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    private const string TokenPassword = "Passw0rd1";

    [Fact]
    public async Task AuthorizedEndpoint_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/account/get-user");
        await CheckResponse(response, 401);
    }

    [Fact]
    public async Task AuthorizedEndpoint_WithTamperedToken_Returns401()
    {
        var username = "jwt_user01";
        await Register(username, "51", TokenPassword);

        var token = await FetchAccessToken(username);
        var tampered = TamperPayload(token);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await Client.GetAsync("/api/account/get-user");
        await CheckResponse(response, 401);
    }

    [Fact]
    public async Task AuthorizedEndpoint_WithValidToken_Returns200()
    {
        var username = "jwt_user02";
        await Register(username, "52", TokenPassword);
        await Login(username, TokenPassword);

        var user = await GetUser();
        Assert.Equal(username, user["username"]);
    }

    [Fact]
    public async Task Login_Rejections_Answer401WithTheSameNeutralMessage()
    {
        await Register("jwt_user03", "53", TokenPassword);

        var responses = new[]
        {
            await PostToken("ghost_user03", TokenPassword),
            await PostToken("jwt_user03", "WrongPassw0rd1"),
        };

        var messages = new List<string?>();

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await CheckResponse<object>(response, 4);

            Assert.Equal(401, result!.Code);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));

            messages.Add(result.Message);
        }

        Assert.Equal(messages[0], messages[1]);
    }

    [Fact]
    public async Task ChangeOvpnPassword_WrongPortalPassword_KeepsItsGenericMessage()
    {
        await Register("jwt_user04", "54", TokenPassword);

        int account_id;

        using (var scope = ServiceProvider.CreateScope())
        {
            var panel_user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
            var vpn_account_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();

            var user_id = await panel_user_repo.GetActiveUserId("jwt_user04");

            await vpn_account_repo.Save(new VpnAccountEntity
            {
                UserId = user_id!.Value,
                RealmId = 1,
                Username = "jwt_user04",
                VpnPassword = "VpnPassword",
            });

            account_id = (await vpn_account_repo.GetAccount("jwt_user04"))!.Id;
        }

        await Login("jwt_user04", TokenPassword);

        var response = await Client.PostAsJsonAsync("/api/vpn/change-ovpn", new
        {
            accountId = account_id,
            token = "wrong-token",
            password = "NewPassw0rd1",
        });

        var result = await CheckResponse<object>(response, 40);

        Assert.Equal(400, result!.Code);
        Assert.Equal("عملیات ناموفق بود!", result.Message);
    }

    private Task<HttpResponseMessage> PostToken(string username, string password)
    {
        return Client.PostAsJsonAsync("/api/auth/token", new
        {
            Username = username,
            Password = password,
        });
    }

    private async Task<string> FetchAccessToken(string username)
    {
        var response = await PostToken(username, TokenPassword);
        var result = await CheckResponseObject(response);
        return result["access_token"] ?? throw new Exception("access_token missing");
    }

    /// <summary>
    /// Flips one character inside the base64url payload segment so the token
    /// structure stays valid but the signature no longer matches.
    /// </summary>
    private static string TamperPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) throw new Exception($"not a JWS token: {token}");

        var payload = new StringBuilder(parts[1]);
        var index = payload.Length / 2;
        payload[index] = payload[index] switch
        {
            'A' => 'B',
            _ => 'A',
        };

        return $"{parts[0]}.{payload}.{parts[2]}";
    }
}
