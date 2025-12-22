using PhotonBypass.Test.Initializer.OutSourceManager;
using Renci.SshNet;
using System.Diagnostics;

namespace PhotonBypass.Test.Initializer;

internal class MikrotikInitializer : IOutSourceInitializer, IOutSourceLevelService
{
    private const int TimeoutSeconds = 120;

    private static Dictionary<string, string> HostIps = new()
    {
        { "Mikrotik-Base", "192.168.56.11" }
    };

    public static string GetIp(string key)
    {
        return HostIps[key];
    }

    public async Task Initialize(string key)
    {
        await Clear(key);
        await Start(key);
        await Check(key);
    }

    public async Task Check(string key)
    {
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalSeconds < TimeoutSeconds)
        {
            try
            {
                using var client = new SshClient(GetIp(key), "admin", "admin");
                await client.ConnectAsync(CancellationToken.None);

                if (client.IsConnected)
                {
                    return;
                }
            }
            catch
            {
                await Task.Delay(3000);
            }
        }

        throw new TimeoutException("MikroTik did not become ready.");
    }

    public async Task Clear(string key)
    {
        await PowerOff(key);
        await RestoreSnapshot(key);
    }

    private static Task<string> List() => Run($"list vms");

    private static Task<string> Start(string key) => Run($"startvm \"{key}\" --type headless");

    private static Task<string> RestoreSnapshot(string key) => Run($"snapshot \"{key}\" restore clean-test-state");

    private static Task<string> Clone(string key) => Run($"clonevm Mikrotik-Base --name \"{key}\" --register");

    private static Task<string> PowerOff(string key) => Run($"controlvm \"{key}\" poweroff", "is not currently running");

    private static Task<string> Drop(string key) => Run($"unregistervm \"{key}\" --delete", "Could not find a registered machine named");

    private static async Task<string> Run(string args, params string[] ignores)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "VBoxManage",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            if (!ignores.Any(error.Contains))
            {
                throw new Exception(error);
            }
        }

        return await process.StandardOutput.ReadToEndAsync();
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<MikrotikInitializer>();
    }
}