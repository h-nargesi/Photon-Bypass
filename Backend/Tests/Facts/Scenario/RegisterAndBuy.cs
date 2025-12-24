using PhotonBypass.Result;
using PhotonBypass.Test.Initializer;
using System.Text.Json;

namespace PhotonBypass.Test.Facts.Scenario;

public class RegisterAndBuy(ProgramLevelInitializer.Factory factory) : ProgramLevelInitializer(factory)
{
    [Fact]
    public async Task GetPrices()
    {
        var response = await Client.GetAsync("/api/basics/prices");

        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var result = JsonSerializer.Deserialize<ApiResult>(content);
            if (result != null)
                throw new Exception(result.Message);
        }

        response.EnsureSuccessStatusCode();
    }
}