using PhotonBypass.Test.Initializer;
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

    private async Task<string> FetchAccessToken(string username)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/token", new
        {
            Username = username,
            Password = TokenPassword,
        });
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
