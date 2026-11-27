using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Hosted;
using Serilog;

namespace PhotonBypass.ServerBridge.HostChecks;

/// <summary>
/// Host-capability checks G1–G3 (Docs/25 D11): the three things only visible
/// from the real panel host — outbound TCP to the SSH ports (G1), binding a
/// listener on the loopback address (G2) and how long the worker lives (G3).
/// They run once at every start and on demand through
/// <c>POST /api/server/host-checks</c>, and log their answer with the fixed
/// <c>host-check</c> prefix so the first publish answers in the log file.
/// </summary>
public interface IHostCapabilityChecks
{
    Task<HostCheckReport> RunAsync(CancellationToken ct = default);
}

internal sealed class HostCapabilityChecks(
    IServiceScopeFactory scope_factory,
    IOptions<ServerBridgeOptions> options,
    TimeProvider time_provider) : IHostedService, IHostCapabilityChecks, IDisposable
{
    private readonly StoppingToken stopping = new();

    private Task worker = Task.CompletedTask;

    private DateTimeOffset? started_at;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        started_at = time_provider.GetUtcNow();

        Log.Information("{0}", HostCheckLogLines.WorkerStarted(
            Environment.ProcessId, Environment.MachineName, RuntimeInformation.OSDescription));

        // the probes must not delay the start of the host
        worker = Task.WhenAll(
            RunInBackgroundAsync(stopping.Token),
            HeartbeatAsync(stopping.Token));

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // the host may stop after its run loop already did: the second stop is a no-op
        if (!stopping.TryStop()) return;

        try
        {
            await worker;
        }
        catch (OperationCanceledException) when (stopping.IsStopped)
        {
        }
    }

    public async Task<HostCheckReport> RunAsync(CancellationToken ct = default)
    {
        var targets = await LoadTargetsAsync();
        var timeout = TimeSpan.FromSeconds(options.Value.SshConnectTimeoutSeconds);

        // parallel: the summary line is written at most one connect-timeout later
        var results = await Task.WhenAll(targets.Select(target => ProbeTcpAsync(target, timeout, ct)));

        var (loopback_bind, loopback_port, loopback_error) = ProbeLoopbackBind();

        var report = new HostCheckReport
        {
            Targets = [.. results],
            OutboundTcp = results.Length < 1 ? null : results.All(result => result.Passed),
            LoopbackBind = loopback_bind,
            LoopbackPort = loopback_port,
            Uptime = started_at is { } started ? time_provider.GetUtcNow() - started : TimeSpan.Zero,
            Summary = HostCheckLogLines.Summary(
                results.Length < 1 ? null : results.All(result => result.Passed), loopback_bind, results.Length),
        };

        foreach (var result in results)
        {
            Log.Information("{0}", HostCheckLogLines.Target(result));
        }

        Log.Information("{0}", HostCheckLogLines.LoopbackBind(loopback_bind, loopback_port, loopback_error));
        Log.Information("{0}", report.Summary);

        return report;
    }

    public void Dispose() => stopping.Dispose();

    private async Task RunInBackgroundAsync(CancellationToken ct)
    {
        try
        {
            await RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Log.Error(error, "host-check run failed on start");
        }
    }

    private async Task HeartbeatAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(options.Value.HostCheckHeartbeatMinutes), time_provider);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                Log.Information("{0}", HostCheckLogLines.Heartbeat(
                    started_at is { } started ? time_provider.GetUtcNow() - started : TimeSpan.Zero));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Destinations of G1: <c>HostCheckTargets</c> plus the SSH endpoint of
    /// every stored server row, without duplicates.
    /// </summary>
    private async Task<List<HostCheckTarget>> LoadTargetsAsync()
    {
        var targets = new Dictionary<string, HostCheckTarget>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in options.Value.HostCheckTargets)
        {
            if (HostCheckTarget.TryParse(value, out var target)) targets.TryAdd(target.Key, target);
        }

        foreach (var target in await LoadStoredSshEndpointsAsync())
        {
            targets.TryAdd(target.Key, target);
        }

        return [.. targets.Values];
    }

    private async Task<List<HostCheckTarget>> LoadStoredSshEndpointsAsync()
    {
        try
        {
            using var scope = scope_factory.CreateScope();

            var servers = await scope.ServiceProvider.GetRequiredService<IServerRepository>().GetAllServers();

            return [.. servers.Select(StoredSshEndpoint).OfType<HostCheckTarget>()];
        }
        catch (Exception error)
        {
            // a diagnostic must not break the start: the configured targets are still probed
            Log.Warning("host-check G1 stored server rows could not be read ({0}); only HostCheckTargets are probed",
                error.GetType().Name);

            return [];
        }
    }

    private static HostCheckTarget? StoredSshEndpoint(ServerEntity server)
    {
        try
        {
            var ssh = server.Config?.SshConfig;

            return ssh != null && HostCheckTarget.TryParse($"{server.IpAddress}:{ssh.Port}", out var target)
                ? target
                : null;
        }
        catch (Exception)
        {
            // a row with an unreadable config is skipped; the rest are still probed
            return null;
        }
    }

    private async Task<HostCheckTargetResult> ProbeTcpAsync(HostCheckTarget target, TimeSpan timeout, CancellationToken ct)
    {
        var probe = await TcpProbe.ConnectAsync(target.Host, target.Port, timeout, time_provider, ct);

        return new HostCheckTargetResult
        {
            Target = target.ToString(),
            Passed = probe.Passed,
            Milliseconds = probe.Milliseconds,
            Error = probe.Error,
        };
    }

    private (bool Passed, int Port, string? Error) ProbeLoopbackBind()
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);

            listener.Start();

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            listener.Stop();

            return (true, port, null);
        }
        catch (Exception failure)
        {
            return (false, 0, failure.GetType().Name);
        }
    }
}
