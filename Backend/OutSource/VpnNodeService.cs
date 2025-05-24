using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.ErrorHandler;
using Renci.SshNet;
using Serilog;
using System.Text.RegularExpressions;

namespace PhotonBypass.OutSource;

public partial class VpnNodeService : IVpnNodeService
{
    public async Task<bool> CloseConnection(NasEntity server, string sessionId)
    {
        if (server == null || string.IsNullOrEmpty(sessionId)) return false;

        if (!SessionIdCheck().IsMatch(sessionId))
        {
            return false;
        }

        using var node = await Connect(server);

        var success = node.Execute($"/ppp active remove [find session-id=0x{sessionId}]", out string result);
        if (!success) return false;

        success = node.Execute($"/ppp active print where session-id=0x{sessionId}", out result);
        return success && string.IsNullOrEmpty(result);
    }

    public Task<bool> CloseConnections(IEnumerable<NasEntity> servers, string username, int count)
    {
        //if (servers == null || !servers.Any() || count < 1 || string.IsNullOrEmpty(username)) return false;

        throw new NotImplementedException();
    }

    public async Task<(NasEntity server, IList<UserConnectionBinding> connections)> GetActiveConnections(NasEntity server, string username)
    {
        ArgumentNullException.ThrowIfNull(server, nameof(server));

        if (string.IsNullOrEmpty(username)) return (server, []);

        using var node = await Connect(server);

        var success = node.Execute($"/ppp active print where name=\"{username}\" uptime session-id caller-id limit-bytes-in", out string result);
        if (!success || string.IsNullOrEmpty(result)) return (server, []);

        var connections = ConnectionParse().Matches(result)
            .Select(x => new UserConnectionBinding
            {
                CallerId = x.Groups[1].Value,
                Name = username,
                SessionId = x.Groups[3].Value,
                UpTime = x.Groups[2].Value,
            })
            .ToList();

        return (server, connections);
    }

    public Task<CertContext> GetCertificate(string server)
    {
        /*
         
        /certificate
        add name=CLIENT-TEMPLATE common-name=CLIENT key-usage=tls-client key-size=4096 days-valid=3650 country="IR" state="TH" locality="Tehran" organization="Photon" unit="VPN"
        add name=CLIENT1 copy-from=CLIENT-TEMPLATE common-name=CLIENT1
        sign CLIENT1 ca=LMTCA name=CLIENT1
        export-certificate CLIENT1 export-passphrase="<ovpn-secret>" file-name="CLIENT1"

         */
        throw new NotImplementedException();
    }

    public static async Task<SshClient> Connect(NasEntity server)
    {
        var node = new SshClient(server.IpAddress, 9009, "admin", server.ShhPassword);

        await node.ConnectAsync(CancellationToken.None);

        if (!node.IsConnected)
        {
            node.Dispose();
            throw new UserException("خطای اتصال به سرور!",
                $"Cannont connect to server: {server.Name}={server.IpAddress}");
        }

        return node;
    }

    [GeneratedRegex(@"^[\da-fA-F]+$")]
    private static partial Regex SessionIdCheck();

    [GeneratedRegex(@"\d+.+caller-id=([\.\d""]+) .+uptime=([\w""]*) .+session-id=([\w""]*)( |$)")]
    private static partial Regex ConnectionParse();
}

static class SshExtentions
{
    public static bool Execute(this SshClient node, string command, out string result)
    {
        Log.Information("Execute command on server: {0}\n{1}", node.ConnectionInfo.Host, command);

        var execution = node.RunCommand(command);
        result = execution.Result;

        if (execution.Error != null)
        {
            Log.Error("Error on execute command on server: {0}\n{1}\n{2}", node.ConnectionInfo.Host, command, execution.Error);
            return false;
        }

        return true;
    }
}
