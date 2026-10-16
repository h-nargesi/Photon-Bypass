using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Test.Initializer;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using TokenContext = PhotonBypass.Portal.Context.TokenContext;

namespace PhotonBypass.Test.Facts.Scenario;

/// <summary>
/// Roles wiring (docs/20 phase 1): role claim in the JWT, role/self-service in
/// get-user, and Manager/Admin policy enforcement on a validated principal.
/// </summary>
public class RolesAuthorizationTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Login_AdminTokenCarriesAdminRoleClaim_RegisteredUserCarriesUserRoleClaim()
    {
        var admin_token = await LoginAndGetToken("admin", "admin");
        Assert.Equal(nameof(AccountRole.Admin), RoleClaimOf(admin_token));

        await Register("roles_user01", "90", "Password");

        var user_token = await LoginAndGetToken("roles_user01", "Password");
        Assert.Equal(nameof(AccountRole.User), RoleClaimOf(user_token));
    }

    [Fact]
    public async Task GetUser_ReportsRoleAndSelfServiceFlag()
    {
        // admin: Role=2 + UserType=4 come from the scenario seed (Tests/Data/Sql/DbScenario/init.sql)
        await Login("admin", "admin");
        var admin = await GetUser();
        Assert.Equal("2", admin["role"]);
        Assert.Equal("True", admin["allowSelfService"]);

        // registered user: independent account created with the flag (D11)
        await Register("roles_user02", "91", "Password");
        await Login("roles_user02", "Password");
        var user = await GetUser();
        Assert.Equal("0", user["role"]);
        Assert.Equal("True", user["allowSelfService"]);
    }

    [Fact]
    public async Task Policies_EnforceRoleMatrixOnValidatedPrincipals()
    {
        using var scope = ServiceProvider.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        await Register("roles_user03", "92", "Password");
        var user_token = await LoginAndGetToken("roles_user03", "Password");
        var admin_token = await LoginAndGetToken("admin", "admin");

        var user_principal = ValidateToken(user_token);
        var admin_principal = ValidateToken(admin_token);

        // admin passes both (Manager policy includes Admin, docs/20 D10)
        (await authorization.AuthorizeAsync(admin_principal, null, ServiceFactory.AdminPolicy))
            .Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(admin_principal, null, ServiceFactory.ManagerPolicy))
            .Succeeded.Should().BeTrue();

        // a plain user passes neither
        (await authorization.AuthorizeAsync(user_principal, null, ServiceFactory.AdminPolicy))
            .Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(user_principal, null, ServiceFactory.ManagerPolicy))
            .Succeeded.Should().BeFalse();

        // a synthetic manager principal (scenario managers are seeded by the
        // phase-4 tests) passes Manager only
        var manager_principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, nameof(AccountRole.Manager))], "Bearer"));
        (await authorization.AuthorizeAsync(manager_principal, null, ServiceFactory.ManagerPolicy))
            .Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(manager_principal, null, ServiceFactory.AdminPolicy))
            .Succeeded.Should().BeFalse();
    }

    private async Task<string> LoginAndGetToken(string username, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = username,
            Password = password,
        });
        var data = await CheckResponseObject(response);
        return data["access_token"] ?? throw new Exception("access_token missing");
    }

    private static string RoleClaimOf(string token)
    {
        // serialized either as the short "role" name or the long ClaimTypes.Role
        // URI depending on the handler's claim-type map; both are valid JWTs
        return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .FirstOrDefault(claim => claim.Type is "role" or ClaimTypes.Role)?.Value
            ?? throw new Exception("role claim missing from token");
    }

    /// <summary>
    /// Same handler + parameters the JwtBearer middleware uses, so the inbound
    /// "role" → ClaimTypes.Role mapping is exercised for real.
    /// </summary>
    private static ClaimsPrincipal ValidateToken(string token)
    {
        return new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = IdentityHelper.Issuer,
            ValidateAudience = true,
            ValidAudience = IdentityHelper.Issuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = IdentityHelper.Key,
            ClockSkew = TimeSpan.Zero,
        }, out _);
    }
}
