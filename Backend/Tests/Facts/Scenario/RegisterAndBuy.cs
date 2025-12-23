using Microsoft.AspNetCore.Mvc.Testing;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.Scenario;

public class RegisterAndBuy(WebApplicationFactory<PortalProgram> factory) : ProgramLevelInitializer(factory)
{
    private const string TestPackageMikrotik = "Mikrotik-Scenario";
    private const string TestPackageDatabase = "DbScenario";
    
    [Fact]
    public async Task GetPrices()
    {
        using var scope = App.Services.CreateScope();
        var init_mikrotik = Task.CompletedTask; //scope.Register<MikrotikInitializer>(TestPackageMikrotik, this);
        var init_database = scope.Register<LocalDatabaseInitializer>(TestPackageDatabase, this);

        await Task.WhenAll(init_mikrotik, init_database);
        
        var response = await Client.GetAsync("/api/basics/prices");

        response.EnsureSuccessStatusCode();
    }
}