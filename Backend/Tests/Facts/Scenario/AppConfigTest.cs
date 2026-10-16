using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class AppConfigTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task AppConfig_IsPublic_WithoutToken()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/basics/app-config");
        var config = await CheckResponseObject(response);

        Assert.Equal("True", config["allowRegistration"]);
    }
}
