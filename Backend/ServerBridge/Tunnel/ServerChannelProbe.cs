using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.HostChecks;
using Serilog;

namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// The channel steps of the admin connection test (Docs/25 D8–D9), on a
/// throw-away session: <c>tcp</c>, <c>host-key</c>, <c>auth</c> and — on the
/// tunnel transport — <c>forward</c>. The tunnel transport stops on an empty
/// pin and reports the fingerprint to register; the direct transport goes on
/// unpinned. A mismatching pin stops both.
/// </summary>
internal sealed class ServerChannelProbe(
    ISshConnector connector,
    IOptions<ServerBridgeOptions> options,
    TimeProvider time_provider) : IServerChannelProbe
{
    private const string NoSshConfigurationDetail = "پیکربندی SSH این سرور تنظیم نشده است!";

    private const string InvalidSshPortDetail = "پورت SSH نامعتبر است!";

    private const string ServiceAddressRequiredDetail = "در حالت تانل، آدرس سرویس باید `127.0.0.1` با پورت مشخص باشد!";

    private const string PinNotRegisteredDetail =
        "کلید میزبان این سرور ثبت نشده است! اثر انگشت کشف‌شده را در فیلد «کلید میزبان» ثبت کنید و دوباره تست کنید.";

    public async Task<ChannelProbeResult> ProbeAsync(ServerEntity server, CancellationToken ct = default)
    {
        var settings = options.Value;

        var timeout = TimeSpan.FromSeconds(settings.SshConnectTimeoutSeconds);

        // Config decrypts and parses on every access: read it once per call
        var configuration = server.Config;

        var tunnel = configuration?.Transport == ServerTransport.SshTunnel;

        var ssh = configuration?.SshConfig;

        var steps = new List<ConnectionStepModel>();

        if (ssh == null)
        {
            steps.Add(ConnectionStepModel.Fail(ConnectionStepModel.Tcp, NoSshConfigurationDetail));

            return Result(server, steps);
        }

        if (!HostCheckTarget.TryParse($"{server.IpAddress}:{ssh.Port}", out var target))
        {
            steps.Add(ConnectionStepModel.Fail(ConnectionStepModel.Tcp, InvalidSshPortDetail));

            return Result(server, steps);
        }

        var tcp = await TcpProbe.ConnectAsync(target.Host, target.Port, timeout, time_provider, ct);

        steps.Add(tcp.Passed
            ? ConnectionStepModel.Pass(ConnectionStepModel.Tcp,
                $"اتصال TCP برقرار شد (پورت {target.Port}) در {tcp.Milliseconds} میلی‌ثانیه")
            : ConnectionStepModel.Fail(ConnectionStepModel.Tcp,
                $"اتصال TCP برقرار نشد (پورت {target.Port}): {tcp.Error}"));

        if (!tcp.Passed) return Result(server, steps);

        var pin = SshTunnelConfig.NormalizeHostKeyPin(ssh.HostKeySha256);

        var policy = !tunnel
            ? HostKeyPolicy.Optional
            : pin.Length < 1 ? HostKeyPolicy.Discover : HostKeyPolicy.Required;

        SshEndpoint endpoint;

        try
        {
            endpoint = SshEndpointFactory.Create(server, configuration!, policy, settings);
        }
        catch (UserException error)
        {
            steps.Add(ConnectionStepModel.Fail(ConnectionStepModel.Auth, error.UserMessage));

            return Result(server, steps);
        }

        ISshChannel channel;

        try
        {
            channel = await connector.ConnectAsync(endpoint, ct);
        }
        catch (SshConnectException error)
        {
            steps.Add(FailedStep(error));

            return Result(server, steps, error.PresentedHostKey);
        }

        var host_key = channel.HostKey;

        steps.Add(ConnectionStepModel.Pass(ConnectionStepModel.HostKey, Describe(host_key)));

        if (host_key.Status == HostKeyStatus.Discovered)
        {
            channel.Dispose();

            // nothing is proven until the discovered key is pinned: the test stops before authentication
            steps.Add(ConnectionStepModel.Fail(ConnectionStepModel.Auth, PinNotRegisteredDetail));

            return Result(server, steps, host_key.Sha256);
        }

        steps.Add(ConnectionStepModel.Pass(ConnectionStepModel.Auth, endpoint.PrivateKey != null
            ? $"احراز هویت با کلید خصوصی موفق بود ({host_key.KeyName})"
            : $"احراز هویت با پسورد موفق بود ({host_key.KeyName})"));

        // a tunnel to a server without a management service (a NAS reached for its scripts or OpenVPN files) has nothing to forward
        if (!tunnel || !NeedsService(server.Features)) return Result(server, steps, host_key.Sha256, null, Release(channel));

        var forward = await ProbeForwardAsync(channel, configuration!.WebApiConfig, timeout, ct);

        steps.Add(forward.Step);

        return Result(server, steps, host_key.Sha256, forward.LocalPort, Release(channel));
    }

    private static bool NeedsService(ServerFeature features) =>
        (features & (ServerFeature.Xui | ServerFeature.UserManager)) != 0;

    private async Task<(ConnectionStepModel Step, int? LocalPort)> ProbeForwardAsync(
        ISshChannel channel, WebApiConfig? web_config, TimeSpan timeout, CancellationToken ct)
    {
        if (web_config?.Port is not { } remote_port)
        {
            return (ConnectionStepModel.Fail(ConnectionStepModel.Forward, ServiceAddressRequiredDetail), null);
        }

        try
        {
            var local_port = channel.StartLocalForward(remote_port);

            // proves the loopback listener works; only the service step proves the server allows the channel
            using var probe = new TcpClient();
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);

            limit.CancelAfter(timeout);

            await probe.ConnectAsync(IPAddress.Loopback, local_port, limit.Token);

            return (ConnectionStepModel.Pass(ConnectionStepModel.Forward,
                $"کانال باز شد (پورت محلی {local_port}، پورت سرویس {remote_port})"), local_port);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            return (ConnectionStepModel.Fail(ConnectionStepModel.Forward,
                $"{SshErrors.ConnectMessage} ({error.GetType().Name})"), null);
        }
    }

    private static ConnectionStepModel FailedStep(SshConnectException error)
    {
        var message = SshErrors.ToUserException(error).UserMessage;

        var step = error.Failure is SshConnectFailure.HostKeyMismatch or SshConnectFailure.HostKeyMissing
            ? ConnectionStepModel.HostKey
            : ConnectionStepModel.Auth;

        var detail = error.Failure is SshConnectFailure.Timeout or SshConnectFailure.Connect or SshConnectFailure.Authentication
            ? $"{message} ({error.Step}, {error.ElapsedMilliseconds} ms)"
            : message;

        return ConnectionStepModel.Fail(step, detail);
    }

    private static string Describe(HostKeyCheck host_key)
    {
        return host_key.Status switch
        {
            HostKeyStatus.Unpinned => $"{host_key.Sha256} ({host_key.KeyName}) — بدون پین",
            HostKeyStatus.Discovered => $"{host_key.Sha256} ({host_key.KeyName}) — کشف شد؛ پیش از احراز هویت متوقف شد",
            _ => $"{host_key.Sha256} ({host_key.KeyName}) — مطابق مقدار ثبت‌شده",
        };
    }

    private static Func<ValueTask> Release(ISshChannel channel) =>
        () =>
        {
            channel.Dispose();

            return ValueTask.CompletedTask;
        };

    private static ChannelProbeResult Result(
        ServerEntity server, List<ConnectionStepModel> steps, string? host_key = null,
        int? forwarded_local_port = null, Func<ValueTask>? release = null)
    {
        var last = steps[^1];

        if (!last.Passed)
        {
            Log.Warning("Connection test stopped on {0} for server ({1}:{2}): {3}",
                last.Step, server.Id, server.Name, last.Detail);
        }

        return new ChannelProbeResult(steps, host_key, forwarded_local_port, release);
    }
}
