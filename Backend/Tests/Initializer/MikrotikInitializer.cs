using System.Diagnostics;
using Renci.SshNet;

namespace PhotonBypass.Test.Initializer;

internal class MikrotikInitializer : IOutSourceInitializer, IOutSourceLevelService
{
    private const int TimeoutSeconds = 120;
    
    public async Task Initialize(string key)
    {
        await Start(key);
        await RestoreSnapshot(key);
        await Check(key);
    }

    public async Task Check(string key)
    {
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalSeconds < TimeoutSeconds)
        {
            try
            {
                using var client = new SshClient("127.0.0.1", "admin", "admin");
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

    public Task Clear(string key) => PowerOff(key);

    private static Task Start(string key)
    {
        return Run($"startvm {key} --type headless");
    }

    private static Task RestoreSnapshot(string key)
    {
        return Run($"snapshot {key} restore clean-test-state");
    }

    private static Task PowerOff(string key)
    {
        return Run($"controlvm {key} poweroff");
    }

    private static Task Run(string args)
    {
        return Task.Run(() =>
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
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception(process.StandardError.ReadToEnd());
            }
        });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddTransient<MikrotikInitializer>();
    }
}