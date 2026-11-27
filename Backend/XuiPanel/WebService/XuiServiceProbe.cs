using System.Net.Sockets;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Model;

namespace PhotonBypass.Xui.WebService;

/// <summary>
/// The service step of the connection test for an x-ui server (Docs/25 D9): a
/// real login through the same contract the adapter uses. On the tunnel
/// transport it rides the test's own port-forward — never the pooled session.
/// </summary>
internal sealed class XuiServiceProbe(XuiHttpClientProvider provider) : IServiceProbe
{
    private const string NoWebConfigurationDetail = "پیکربندی وب پنل x-ui تنظیم نشده است!";

    public async Task<ConnectionStepModel> ProbeAsync(
        ServerEntity server, int? forwarded_local_port, CancellationToken ct = default)
    {
        var config = server.Config?.WebApiConfig;

        if (config == null)
        {
            return ConnectionStepModel.Fail(ConnectionStepModel.Service, NoWebConfigurationDetail);
        }

        try
        {
            if (forwarded_local_port is { } port)
            {
                using var handler = new SocketsHttpHandler { UseProxy = false };
                using var http = provider.CreateOwned(handler);

                await XuiLogin.LoginAsync(http, XuiLogin.BaseUrl(config, port), config, ct);
            }
            else
            {
                using var http = provider.Get(server);

                await XuiLogin.LoginAsync(http, XuiLogin.BaseUrl(config), config, ct);
            }

            return ConnectionStepModel.Pass(ConnectionStepModel.Service, "ورود به پنل x-ui موفق بود.");
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return ConnectionStepModel.Fail(ConnectionStepModel.Service, DescribeFailure(error, forwarded_local_port != null));
        }
    }

    private static string DescribeFailure(Exception error, bool tunnel)
    {
        if (XuiLoginFailure.IsRejection(error)) return "اعتبارنامه‌ی پنل x-ui رد شد!";

        if (XuiTransportDeath.Is(error))
        {
            // through a tunnel a closed connection has two indistinguishable causes (Docs/25 appendix B, finding 3)
            return tunnel
                ? "ورود به پنل x-ui ممکن نشد! یا سرور اجازه‌ی عبور ترافیک از تانل را نداد! یا سرویس مقصد ارتباط را بست."
                : "ارتباط با پنل x-ui برقرار نشد!";
        }

        if (error is OperationCanceledException or TimeoutException) return "پاسخی از پنل x-ui دریافت نشد!";

        return "ورود به پنل x-ui ناموفق بود!";
    }
}
