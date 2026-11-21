using PhotonBypass.Test.Initializer.OutSourceManager;
using Renci.SshNet;

namespace PhotonBypass.Test.Initializer;

internal class MikrotikInitializer(VirtualBoxManager vbox) : IOutSourceInitializer, IOutSourceLevelService
{
    private const int TimeoutSeconds = 120;

    public static string GetIp(string key)
    {
        return TestVmOptions.Get(key).Ip;
    }

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
                using var client = new SshClient(vm.Ip, vm.Username, vm.Password);
                await client.ConnectAsync(CancellationToken.None);

                if (client.IsConnected)
                {
                    return;
                }
            }
            catch
            {
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException("MikroTik did not become ready.");
    }

    public async Task Clear(string key)
    {
        await vbox.PowerOff(key);
        await vbox.RestoreSnapshot(key);
    }

    private Task<string> List() => vbox.Run($"list vms");

    private Task<string> Clone(string key) => vbox.Run($"clonevm Mikrotik-Base --name \"{key}\" --register");

    private Task<string> Drop(string key) => vbox.Run($"unregistervm \"{key}\" --delete", "Could not find a registered machine named");

    public static void CreateInstance(IServiceCollection services)
    {
        VirtualBoxManager.CreateInstance(services);
        services.AddTransient<MikrotikInitializer>();
    }
}
