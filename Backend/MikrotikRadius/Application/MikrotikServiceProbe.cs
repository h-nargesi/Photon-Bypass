using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net;

namespace PhotonBypass.Mikrotik.Radius.Application;

/// <summary>
/// The service step of the connection test for a User Manager server
/// (Docs/25 D9): opens the RouterOS API and runs one read command. On the
/// tunnel transport it rides the test's own port-forward — never the pooled
/// session.
/// </summary>
internal sealed class MikrotikServiceProbe(
    TikConnectionFactory connection_factory,
    IOptions<ServerBridgeOptions> options) : IServiceProbe
{
    private const int ApiDefaultPort = 8728;

    private const int ApiSslDefaultPort = 8729;

    private const string LoopbackHost = "127.0.0.1";

    private const string IdentityCommand = "/system/identity/print";

    public async Task<ConnectionStepModel> ProbeAsync(
        ServerEntity server, int? forwarded_local_port, CancellationToken ct = default)
    {
        var config = server.Config?.WebApiConfig;

        if (config == null)
        {
            return ConnectionStepModel.Fail(ConnectionStepModel.Service, "پیکربندی وب User Manager تنظیم نشده است!");
        }

        var tunnel = forwarded_local_port != null;

        var timeout = TimeSpan.FromSeconds(options.Value.Tik4NetOpenTimeoutSeconds);

        using var connection = connection_factory(config.Ssl ? TikConnectionType.ApiSsl : TikConnectionType.Api);

        try
        {
            var port = forwarded_local_port ?? config.Port ?? (config.Ssl ? ApiSslDefaultPort : ApiDefaultPort);

            await TikConnectionOpener.OpenAsync(
                connection,
                tunnel ? LoopbackHost : config.HostName ?? string.Empty,
                port,
                config.Username ?? string.Empty,
                config.Password ?? string.Empty,
                timeout,
                ct);

            var identity = connection.CreateCommand(IdentityCommand).ExecuteScalar("name");

            return ConnectionStepModel.Pass(ConnectionStepModel.Service,
                string.IsNullOrEmpty(identity)
                    ? "ورود به User Manager موفق بود."
                    : $"ورود به User Manager موفق بود (نام دستگاه: {identity}).");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TikConnectionLoginException)
        {
            return ConnectionStepModel.Fail(ConnectionStepModel.Service, "اعتبارنامه‌ی User Manager رد شد!");
        }
        catch (TimeoutException)
        {
            return ConnectionStepModel.Fail(ConnectionStepModel.Service, "پاسخی از User Manager دریافت نشد!");
        }
        catch (Exception)
        {
            // through a tunnel a closed connection has two indistinguishable causes (Docs/25 appendix B, finding 3)
            return ConnectionStepModel.Fail(ConnectionStepModel.Service, tunnel
                ? "ورود به User Manager ممکن نشد! یا سرور اجازه‌ی عبور ترافیک از تانل را نداد! یا سرویس مقصد ارتباط را بست."
                : "ارتباط با User Manager برقرار نشد!");
        }
    }
}
