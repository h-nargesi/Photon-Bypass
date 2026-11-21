using System.Text.Json;
using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Polling helpers for the live data-plane tier: the panel flushes its
/// per-client counters roughly every 10 seconds (Docs/17 §4), so every
/// counter assertion waits for a settled value instead of a fixed sleep.
/// </summary>
internal static class XuiPanelTrafficProbe
{
    private static readonly TimeSpan SettleWindow = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan SettlePollInterval = TimeSpan.FromSeconds(4);

    private static readonly TimeSpan SettleCap = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Waits until the client's total counter is stable across a flush
    /// boundary and has reached at least <paramref name="minimum"/> bytes,
    /// then returns the settled value.
    /// </summary>
    public static async Task<long> SettledTotalAsync(
        Lazy<IXuiApi> api, ServerEntity server, string email, long minimum)
    {
        await Task.Delay(SettleWindow);

        var last = -1L;
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < SettleCap)
        {
            var current = (await ReadStatsAsync(api, server, email)).Total;

            if (current == last && current >= minimum)
            {
                return current;
            }

            last = current;
            await Task.Delay(SettlePollInterval);
        }

        throw new TimeoutException(
            $"The panel counters for '{email}' did not settle at >= {minimum} bytes within {SettleCap}.");
    }

    public static async Task<XuiClientTrafficModel> ReadStatsAsync(
        Lazy<IXuiApi> api, ServerEntity server, string email)
    {
        return (await api.Value.ListClientTraffic(server)).Single(stats => stats.Email == email);
    }

    public static async Task PollUntilAsync(Func<Task<bool>> condition, string what, TimeSpan? cap = null)
    {
        var start = DateTime.UtcNow;
        var limit = cap ?? TimeSpan.FromSeconds(90);

        while (DateTime.UtcNow - start < limit)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        throw new TimeoutException($"Timed out waiting for: {what}");
    }

    /// <summary>
    /// Moves real bytes through the client's VLESS tunnel: the destination is
    /// the panel's own HTTP port and the request carries an oversized padding
    /// header, so the up counter grows by roughly the padding size and the
    /// error response grows the down counter.
    /// </summary>
    public static async Task PushTrafficAsync(
        Lazy<IXuiApi> api, ServerEntity server, string email, int padding_bytes)
    {
        var client = await api.Value.GetClient(server, email)
            ?? throw new Exception($"The panel client was not found for traffic generation: ({email})");

        var inbound = await api.Value.GetInbound(server);
        var path = JsonDocument.Parse(inbound.StreamSettings!).RootElement
            .GetProperty("wsSettings").GetProperty("path").GetString()
            ?? throw new Exception("The test inbound carries no websocket path.");

        var host = XuiPanelTestServer.Vm.Ip;

        await using var tunnel = await VlessWsClient.ConnectAsync(
            host, inbound.Port, path, client.Uuid, host, XuiPanelTestServer.Vm.Port!.Value);

        await tunnel.SendUpAsync(
            $"GET / HTTP/1.1\r\nHost: {host}\r\nX-Pad: {new string('A', padding_bytes)}\r\nConnection: close\r\n\r\n");

        await tunnel.DrainDownAsync(TimeSpan.FromSeconds(30));
    }
}
