using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Initializer;

internal class XuiPanelInitializer(VirtualBoxManager vbox) : IOutSourceInitializer, IOutSourceLevelService
{
    public const string VmKey = "Xui-Test";

    private const int TimeoutSeconds = 120;

    public async Task Initialize(string key)
    {
        await Clear(key);
        await vbox.StartHeadless(key);
        await Check(key);
    }

    public async Task Check(string key)
    {
        var vm = TestVmOptions.Get(key);
        var start = DateTime.UtcNow;
        await Task.Delay(5000);

        while ((DateTime.UtcNow - start).TotalSeconds < TimeoutSeconds)
        {
            try
            {
                if (await IsPanelUp(vm))
                {
                    return;
                }
            }
            catch
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("The x-ui panel did not become ready.");
    }

    public async Task Clear(string key)
    {
        await vbox.PowerOff(key);
        await vbox.RestoreSnapshot(key);
    }

    private static async Task<bool> IsPanelUp(TestVmEndpoint vm)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        using var content = new StringContent(
            JsonSerializer.Serialize(new { username = vm.Username, password = vm.Password }),
            Encoding.UTF8,
            "application/json");

        using var response = await http.PostAsync($"http://{vm.Ip}:{vm.Port}/login", content);

        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<LoginEnvelope>();

        return envelope is { Success: true };
    }

    private sealed class LoginEnvelope
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }

    public static void CreateInstance(IServiceCollection services)
    {
        VirtualBoxManager.CreateInstance(services);
        services.AddTransient<XuiPanelInitializer>();
    }
}
