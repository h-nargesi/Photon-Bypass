using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// The x-ui panel refuses <c>delClient</c> when it would leave the inbound
/// with zero clients ("no client remained in Inbound" — discovered live by
/// this tier on v1.14.0). The suite therefore pins one carrier client on the
/// panel for the whole run so per-test cleanup can always remove its own
/// client. The snapshot restore at the start of the next run removes it.
/// </summary>
internal static class XuiPanelCarrier
{
    private static readonly SemaphoreSlim Gate = new(1);

    private static string? email;

    /// <summary>The carrier email of this test run, or null while no test needed one yet.</summary>
    public static string? Email => email;

    public static async Task AttachAsync(Lazy<IXuiApi> api, ServerEntity server)
    {
        await Gate.WaitAsync();

        try
        {
            email ??= XuiPanelTestServer.Email("carrier");

            if (await api.Value.GetClient(server, email) == null)
            {
                await api.Value.AddClient(server,
                    new XuiClientModel { Email = email, Uuid = Guid.NewGuid().ToString() });
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
