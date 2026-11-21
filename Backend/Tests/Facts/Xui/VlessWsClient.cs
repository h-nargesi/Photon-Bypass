using System.Buffers.Binary;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Minimal VLESS-over-WebSocket client for the data-plane integration tier
/// (Docs/23 §4.4): speaks just enough of the VLESS v0 wire protocol with BCL
/// primitives (no extra package) to move real bytes through the test inbound
/// (VLESS+ws, no TLS — Scripts/xui-test-vm.sh). The tunnel destination is the
/// panel's own HTTP port: an oversized request header yields deterministic up
/// bytes and the error response yields down bytes, both counted by the panel's
/// per-client statistics.
/// </summary>
/// <remarks>
/// xray does not flush the 2-byte VLESS response header until it has proxied
/// data to send (verified live on xray 26.10.10): waiting for the header
/// before sending payload deadlocks. The header is therefore stripped from
/// the first received bytes here, and payload must follow the request header
/// immediately.
/// </remarks>
internal sealed class VlessWsClient : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();

    private bool response_header_pending = true;

    private VlessWsClient()
    {
    }

    public static async Task<VlessWsClient> ConnectAsync(
        string panel_host, int inbound_port, string ws_path, string uuid, string dest_host, int dest_port)
    {
        var client = new VlessWsClient();

        await client.socket.ConnectAsync(
            new Uri($"ws://{panel_host}:{inbound_port}{ws_path}"), CancellationToken.None);

        await client.socket.SendAsync(BuildRequest(uuid, dest_host, dest_port),
            WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);

        return client;
    }

    /// <summary>Sends payload bytes through the tunnel (counts as client up traffic).</summary>
    public async Task SendUpAsync(string payload)
    {
        await SendUpAsync(Encoding.ASCII.GetBytes(payload));
    }

    public async Task SendUpAsync(byte[] payload)
    {
        await socket.SendAsync(payload, WebSocketMessageType.Binary,
            endOfMessage: true, CancellationToken.None);
    }

    /// <summary>
    /// Reads proxied payload until the tunnel closes (counts as client down
    /// traffic) and returns the total number of bytes seen. The VLESS response
    /// header of the first frame is stripped and validated transparently.
    /// </summary>
    public async Task<long> DrainDownAsync(TimeSpan cap)
    {
        var total = 0L;
        var buffer = new byte[16 * 1024];
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < cap)
        {
            int count;

            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                count = await ReadAsync(buffer, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                continue;
            }
            catch (WebSocketException)
            {
                break;
            }

            if (count == 0)
            {
                break;
            }

            total += count;
        }

        return total;
    }

    /// <summary>
    /// One byte of tunnel traffic — enough activity for the panel's online
    /// tracking while the destination (sshd, waiting on its pre-auth line)
    /// keeps the tunnel open indefinitely.
    /// </summary>
    public Task NudgeAsync()
    {
        return SendUpAsync("."u8.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cancellation.Token);
            }
        }
        catch
        {
            // the tunnel may already be gone — closing is best effort
        }

        socket.Dispose();
    }

    private async Task<int> ReadAsync(byte[] buffer, CancellationToken cancellation)
    {
        var result = await socket.ReceiveAsync(buffer, cancellation);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return 0;
        }

        if (!response_header_pending)
        {
            return result.Count;
        }

        // first bytes of the stream: [version][addon-length][addons], then
        // whatever the destination already sent; strip the header (looping
        // across frames — xray may deliver it in its own tiny frame)
        var count = result.Count;

        while (true)
        {
            if (count >= 2)
            {
                var addon_length = buffer[1];

                if (count >= 2 + addon_length)
                {
                    if (buffer[0] != 0x00)
                    {
                        throw new Exception($"The VLESS server answered with an unknown protocol version: ({buffer[0]})");
                    }

                    var data_offset = 2 + addon_length;

                    count -= data_offset;
                    Buffer.BlockCopy(buffer, data_offset, buffer, 0, count);
                    response_header_pending = false;

                    return count;
                }
            }

            var next = await socket.ReceiveAsync(buffer.AsMemory(count), cancellation);

            if (next.MessageType == WebSocketMessageType.Close)
            {
                throw new EndOfStreamException("The VLESS tunnel closed before the response header arrived.");
            }

            count += next.Count;
        }
    }

    private static byte[] BuildRequest(string uuid, string dest_host, int dest_port)
    {
        // the UUID bytes must follow the textual hex order (the "N" form):
        // Guid.ToByteArray() reverses the first three groups and x-ray rejects
        // the id (verified live)
        var uuid_bytes = Convert.FromHexString(Guid.Parse(uuid).ToString("N"));

        using var request = new MemoryStream();

        request.WriteByte(0x00);                                   // version
        request.Write(uuid_bytes);                                 // 16-byte id
        request.WriteByte(0x00);                                   // no addons
        request.WriteByte(0x01);                                   // command: tcp

        Span<byte> port = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(port, (ushort)dest_port);
        request.Write(port);

        request.WriteByte(0x01);                                   // address type: ipv4
        request.Write(IPAddress.Parse(dest_host).GetAddressBytes());

        return request.ToArray();
    }
}
