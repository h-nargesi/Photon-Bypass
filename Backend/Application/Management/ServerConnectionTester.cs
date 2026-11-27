using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using Serilog;

namespace PhotonBypass.Application.Management;

/// <summary>
/// The admin connection test (Docs/25 D8–D9): the channel steps of a server
/// followed, when they all pass, by a real login into one of its management
/// services. The test stops at the first step that does not pass.
/// </summary>
public interface IServerConnectionTester
{
    /// <param name="model">
    /// The unsaved form (its config wins) or a row action carrying only the
    /// id, which tests the stored configuration.
    /// </param>
    Task<ServerConnectionReport> TestAsync(TestServerConnectionModel model);
}

class ServerConnectionTester(
    Lazy<IServerRepository> server_repo,
    Lazy<ISelfServiceGuard> self_service_guard,
    IServerChannelProbe channel_probe,
    [FromKeyedServices(ServerFeature.Xui)] IServiceProbe xui_probe,
    [FromKeyedServices(ServerFeature.UserManager)] IServiceProbe user_manager_probe) : IServerConnectionTester
{
    private const string NoConfigurationMessage = "پیکربندی سرور برای تست در دسترس نیست!";

    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;

    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;

    private IServerChannelProbe ChannelProbe { get; } = channel_probe;

    private IServiceProbe XuiProbe { get; } = xui_probe;

    private IServiceProbe UserManagerProbe { get; } = user_manager_probe;

    public async Task<ServerConnectionReport> TestAsync(TestServerConnectionModel model)
    {
        await RequireAdmin();

        var server = await ResolveTarget(model);

        var steps = new List<ConnectionStepModel>();

        // the throw-away channel stays open until the service step went through its port-forward
        await using (var channel = await ChannelProbe.ProbeAsync(server))
        {
            steps.AddRange(channel.Steps);

            if (channel.Passed && ResolveServiceProbe(server.Features) is { } service_probe)
            {
                steps.Add(await service_probe.ProbeAsync(server, channel.ForwardedLocalPort));
            }

            return new ServerConnectionReport
            {
                Passed = steps.Count > 0 && steps.All(step => step.Passed),
                Steps = steps,
                HostKeySha256 = channel.HostKeySha256,
                MachineName = Environment.MachineName,
            };
        }
    }

    private async Task RequireAdmin()
    {
        // defense in depth behind the Admin policy (same as Docs/20 phase 5)
        var actor = await SelfServiceGuard.Value.ResolveActor();

        if (actor.Role != AccountRole.Admin)
        {
            throw new UserException("مدیریت سرورها فقط برای ادمین مجاز است!",
                $"Server connection test is admin-only (Docs/20 §3): actor={actor.Username}, role={actor.Role}", 403);
        }
    }

    private async Task<ServerEntity> ResolveTarget(TestServerConnectionModel model)
    {
        ServerEntity? stored = null;

        if (model.Id != 0)
        {
            if (model.Id < 1)
            {
                throw new UserException("شناسه سرور نامعتبر است!", $"Connection test requires a positive server id: {model.Id}");
            }

            stored = await ServerRepo.Value.GetServerById(model.Id)
                     ?? throw new UserException("سرور پیدا نشد!", $"Server not found. id:{model.Id}");
        }

        var config_json = model.Config != null
            ? JsonSerializer.Serialize(model.Config)
            : stored?.JsonConfig ?? throw new UserException(NoConfigurationMessage, $"No configuration to test. id:{model.Id}");

        return new ServerEntity
        {
            Id = model.Id,
            Name = Pick(model.Name, stored?.Name),
            IpAddress = Pick(model.IpAddress, stored?.IpAddress),
            OsType = model.OsType ?? stored?.OsType ?? 0,
            Features = model.Features ?? stored?.Features ?? 0,
            JsonConfig = config_json,
        };
    }

    private IServiceProbe? ResolveServiceProbe(ServerFeature features)
    {
        if ((features & ServerFeature.Xui) != 0) return XuiProbe;

        if ((features & ServerFeature.UserManager) != 0) return UserManagerProbe;

        // nothing on this server is reached through a service login
        return null;
    }

    private static string Pick(string? value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value.Trim();
}
