using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using PhotonBypass.Test.Initializer.OutSourceManager;
using Renci.SshNet;

namespace PhotonBypass.Test.Initializer;

internal class MikrotikInitializer(IConfiguration configuration) : IOutSourceInitializer, IOutSourceLevelService
{
    private const int TimeoutSeconds = 120;

    private static readonly Dictionary<string, string> HostIps = new()
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

    private Task<string> List() => Run($"list vms");

    private Task<string> Start(string key) => Run($"startvm \"{key}\" --type headless");

    private Task<string> RestoreSnapshot(string key) => Run($"snapshot \"{key}\" restore clean-test-state");

    private Task<string> Clone(string key) => Run($"clonevm Mikrotik-Base --name \"{key}\" --register");

    private Task<string> PowerOff(string key) => Run($"controlvm \"{key}\" poweroff", "is not currently running");

    private Task<string> Drop(string key) => Run($"unregistervm \"{key}\" --delete", "Could not find a registered machine named");

    private async Task<string> Run(string args, params string[] ignores)
    {
        var vbox_manage_path = configuration["VirtualBoxOptions:AppPath"]
            ?? throw new Exception("VirtualBoxOptions:AppPath was not set.");
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = vbox_manage_path,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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