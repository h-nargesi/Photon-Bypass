using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PhotonBypass.Test.Initializer;

internal sealed class VirtualBoxManager(IConfiguration configuration)
{
    public const string CleanTestStateSnapshot = "clean-test-state";

    public Task<string> StartHeadless(string key) => Run($"startvm \"{key}\" --type headless");

    public Task<string> PowerOff(string key) => Run($"controlvm \"{key}\" poweroff", "is not currently running");

    public Task<string> RestoreSnapshot(string key) => Run($"snapshot \"{key}\" restore {CleanTestStateSnapshot}");

    public async Task<string> Run(string args, params string[] ignores)
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
        services.TryAddTransient<VirtualBoxManager>();
    }
}
