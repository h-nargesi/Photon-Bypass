using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Services;

public static class ServerConfigProtectionWiring
{
    private const string Purpose = "ServerEntity.Config.v1";

    /// <summary>
    /// Encrypts <see cref="ServerEntity.JsonConfig"/> at rest with ASP.NET Data Protection.
    /// Keys are file-based, stored under {app base dir}/data-protection-keys, shared by
    /// nothing else: losing that folder makes stored server credentials unreadable
    /// (recovery = re-entering the configs). Plaintext rows written before this wiring
    /// remain readable; rows are re-encrypted on their next save.
    /// </summary>
    public static void WireServerConfigProtection(this IServiceCollection services)
    {
        var keys_directory = new DirectoryInfo(
            Path.Combine(AppContext.BaseDirectory, "data-protection-keys"));

        var provider = new ServiceCollection()
            .AddDataProtection()
            .PersistKeysToFileSystem(keys_directory)
            .SetApplicationName("Photon-Bypass")
            .Services
            .BuildServiceProvider();

        var protector = provider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose);

        ServerConfigProtection.ProtectImpl = protector.Protect;
        ServerConfigProtection.UnprotectImpl = protector.Unprotect;
    }
}
