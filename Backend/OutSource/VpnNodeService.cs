using System.Text.RegularExpressions;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Tools;
using Renci.SshNet;
using Serilog;
using System.Text;
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
        return CloseAllConnections(servers, username);
    }

    public async Task<bool> CloseAllConnections(IEnumerable<NasEntity> servers, string username)
    {
        if (servers == null || !servers.Any() || string.IsNullOrWhiteSpace(username)) return false;

        var tasks = servers.Where(x => x != null)
            .Select(async server =>
            {
                using var node = await Connect(server);

                var success = node.Execute($"/ppp active remove [find name={username}]", out string result);
                if (!success) return false;

                success = node.Execute($"/ppp active print where name={username}", out result);
                return success && string.IsNullOrEmpty(result);
            });

        var result = await Task.WhenAll(tasks);

        return !result.Any(x => !x);
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

    public async Task GetCertificate(NasEntity server, string username, CertContext default_context)
    {
        ArgumentNullException.ThrowIfNull(server, nameof(server));
        ArgumentNullException.ThrowIfNull(username, nameof(username));
        ArgumentNullException.ThrowIfNull(default_context, nameof(default_context));
        ArgumentNullException.ThrowIfNull(default_context.CertFile, nameof(default_context.CertFile));

        using var node = await Connect(server);

        default_context.PrivateKeyOvpn = HashHandler.GenerateHashCode();

        var success = node.Execute($"/certificate print where name=\"CLIENT_{username}\"", out string result);
        if (!success) throw new Exception("Certificate check failed!");

        if (string.IsNullOrWhiteSpace(result))
        {
            success = node.Execute($"/certificate add name=\"CLIENT_{username}\" copy-from=CLIENT-TEMPLATE common-name=\"CLIENT_{username}\"", out result);
            if (!success) throw new Exception("Certificate generation failed!");

            success = node.Execute($"/certificate sign \"CLIENT_{username}\" ca=LMTCA name=\"CLIENT_{username}\"", out result);
            if (!success) throw new Exception("Certificate signing failed!");
        }

        success = node.Execute($"/file print where name=\"CLIENT_{username}.crt\"", out result);
        if (success && !string.IsNullOrWhiteSpace(result))
        {
            success = node.Execute($"/file remove \"CLIENT_{username}\".crt", out result);
            if (!success) throw new Exception("File remove old failed!");
        }

        success = node.Execute($"/file print where name=\"CLIENT_{username}.key\"", out result);
        if (success && !string.IsNullOrWhiteSpace(result))
        {
            success = node.Execute($"/file remove \"CLIENT_{username}\".key", out result);
            if (!success) throw new Exception("File remove old failed!");
        }

        success = node.Execute($"/certificate export-certificate \"CLIENT_{username}\" export-passphrase=\"{default_context.PrivateKeyOvpn}\" file-name=\"CLIENT_{username}\"", out result);
        if (!success) throw new Exception("Certificate export failed!");

        success = node.Execute($":put [/file get \"CLIENT_{username}\".crt contents]", out result);
        if (!success) throw new Exception("Certificate download cert failed!");
        var client_cert = result;

        success = node.Execute($":put [/file get \"CLIENT_{username}\".key contents]", out result);
        if (!success) throw new Exception("Certificate download key failed!");
        var client_key = result;

        var ovpn_conf_file = Encoding.UTF8.GetString(default_context.CertFile);
        ovpn_conf_file = Replace(ovpn_conf_file, "cert", client_cert);
        ovpn_conf_file = Replace(ovpn_conf_file, "key", client_key);
        default_context.CertFile = Encoding.UTF8.GetBytes(ovpn_conf_file);
    }

    public static async Task<SshClient> Connect(NasEntity server)
    {
        var node = new SshClient(server.IpAddress, 9009, "admin", server.SshPassword);

        await node.ConnectAsync(CancellationToken.None);

        if (!node.IsConnected)
        {
            node.Dispose();
            throw new UserException("خطای اتصال به سرور!",
                $"Cannont connect to server: {server.Name}={server.IpAddress}");
        }

        return node;
    }

    private static string Replace(string source, string type, string value)
    {
        var start = source.IndexOf($"<{type}>");
        if (start < 0) throw new Exception($"The {type} not found in ovpn-conf.");
        start += 2 + type.Length;

        var end = source.IndexOf($"</{type}>", start + 2 + type.Length);
        if (end < 0) throw new Exception($"The {type} not found in ovpn-conf.");

        return new StringBuilder(source)
            .Remove(start, end - start)
            .Insert(start, value)
            .ToString();
    }

    [GeneratedRegex(@"^[\da-fA-F]+$")]
    private static partial Regex SessionIdCheck();

    [GeneratedRegex(@"\d+.+caller-id=([\.\d""]+) .+uptime=([\w""]*) .+session-id=([\w""]*)( |$)")]
    private static partial Regex ConnectionParse();

    [GeneratedRegex("ipsec-secret: (.*)")]
    private static partial Regex GetIpsecSecret();
}

static class SshExtentions
{
    public static bool Execute(this SshClient node, string command, out string result)
    {
        Log.Information("Execute command on server: {0}\n{1}", node.ConnectionInfo.Host, command);

        var execution = node.RunCommand(command);
        result = execution.Result;

        Log.Debug("Result command on server: {0}\n{1}", node.ConnectionInfo.Host, result);

        if (execution.Error != null)
        {
            Log.Error("Error on execute command on server: {0}\n{1}\n{2}", node.ConnectionInfo.Host, command, execution.Error);
            return false;
        }

        return true;
    }
}
