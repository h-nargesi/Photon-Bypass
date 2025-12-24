using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Portal.Context;
using PhotonBypass.Test.Initializer;
using System.Net.Http.Json;

namespace PhotonBypass.Test.Facts.Scenario;

public class RegisterAndBuy(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
{
    [Fact]
    public async Task GetPrices()
    {
        var response = await Client.GetAsync("/api/basics/prices");
        await CheckResponse(response);
    }

    [Fact]
    public async Task Register()
    {
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

        response = await Client.PostAsJsonAsync("/api/auth/token", new TokenContext
        {
            Username = "user01",
            Password = "Password",
        });
        SetToken(await CheckResponse(response));

        response = await Client.GetAsync("/api/account/get-user");
        var user = (await CheckResponse(response)).Data;

        Assert.NotNull(user);
        Assert.Equal("fname lname", user["fullname"].ToString());

        response = await Client.GetAsync("/api/account/full-info");
        user = (await CheckResponse(response)).Data;

        Assert.NotNull(user);
        Assert.Equal("user01", user["username"].ToString());
    }
}