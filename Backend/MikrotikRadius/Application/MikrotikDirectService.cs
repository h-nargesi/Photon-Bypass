using System.Text;
using System.Text.RegularExpressions;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Nas;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Ssh;
using PhotonBypass.Tools;
using Serilog;

namespace PhotonBypass.Mikrotik.Radius.Application;

partial class MikrotikDirectService(ISshHandler handler) : IMikrotikDirectService
{
    public async Task CloseConnection(ServerEntity server, string session_id)
    {
        if (string.IsNullOrEmpty(session_id) || !InjectionRegex.SessionId().IsMatch(session_id))
        {
            throw new Exception($"Invalid server or session-id! ({session_id})");
        }

        using var node = await handler.ConnectTo(server);

        var success = node.Execute($"/ppp active remove [find session-id=0x{session_id}]", out var result);
        if (!success)
        {
            Log.Warning("Closing connection on {0}: {1}", server, result);
            throw new Exception("Closing connection was unsuccessful!");
        }

        success = node.Execute($"/ppp active print where session-id=0x{session_id}", out result);
        if (!success && !string.IsNullOrEmpty(result))
        {
            Log.Warning("Closing connection on {0}: {1}", server, result);
            throw new Exception("Closing connection was unsuccessful!");
        }
    }

    public async Task CloseConnections(IEnumerable<ServerEntity> servers, string username)
    {
        if (!InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception($"Invalid username! ({username})");
        }

        var tasks = servers
            .Select(async server =>
            {
                using var node = await handler.ConnectTo(server);

                var success = node.Execute($"/ppp active remove [find name={username}]", out var result);
                if (!success)
                {
                    Log.Warning("Closing connection on {0}: {1}", server, result);
                    throw new Exception("Closing connection was unsuccessful!");
                }

                success = node.Execute($"/ppp active print where name={username}", out result);
                if (!success && !string.IsNullOrEmpty(result))
                {
                    Log.Warning("Closing connection on {0}: {1}", server, result);
                    throw new Exception("Closing connection was unsuccessful!");
                }
            });

        await Task.WhenAll(tasks);
    }

    public async Task<List<UserConnectionBinding>> GetActivePppConnections(ServerEntity server, string username)
    {
        ArgumentNullException.ThrowIfNull(server, nameof(server));

        if (string.IsNullOrEmpty(username)) return [];

        if (!InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception($"Invalid username! ({username})");
        }

        using var node = await handler.ConnectTo(server);

        var success =
            node.Execute($"/ppp active print where name=\"{username}\" uptime session-id caller-id limit-bytes-in",
                out string result);
        if (!success || string.IsNullOrEmpty(result)) return [];

        var connections = ConnectionParse().Matches(result)
            .Select(x => new UserConnectionBinding
            {
                CallerId = x.Groups[1].Value,
                Username = username,
                SessionId = x.Groups[3].Value,
                UpTime = TimeSpan.ParseExact(x.Groups[4].Value, @"hh\:mm\:ss", null)
            })
            .ToList();

        return connections;
    }

    public async Task GetOVpnCertificate(ServerEntity server, string username, CertContext default_context)
    {
        ArgumentNullException.ThrowIfNull(server, nameof(server));
        ArgumentNullException.ThrowIfNull(username, nameof(username));
        ArgumentNullException.ThrowIfNull(default_context, nameof(default_context));
        ArgumentNullException.ThrowIfNull(default_context.CertFile, nameof(default_context.CertFile));

        if (!InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception($"Invalid username! ({username})");
        }

        using var node = await handler.ConnectTo(server);

        default_context.PrivateKeyOvpn = HashHandler.GenerateHashCode();

        var success = node.Execute($"/certificate print where name=\"CLIENT_{username}\"", out var result);
        if (!success) throw new Exception("Certificate check failed!");

        if (string.IsNullOrWhiteSpace(result))
        {
            success = node.Execute(
                $"/certificate add name=\"CLIENT_{username}\" copy-from=CLIENT-TEMPLATE common-name=\"CLIENT_{username}\"",
                out result);
            if (!success) throw new Exception("Certificate generation failed!");

            success = node.Execute($"/certificate sign \"CLIENT_{username}\" ca=LMTCA name=\"CLIENT_{username}\"",
                out result);
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

        success = node.Execute(
            $"/certificate export-certificate \"CLIENT_{username}\" export-passphrase=\"{default_context.PrivateKeyOvpn}\" file-name=\"CLIENT_{username}\"",
            out result);
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

    public Task SetOVpnCertificate(ServerEntity server, string username, CertContext certificate)
    {
        if (!InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception($"Invalid username! ({username})");
        }

        throw new NotImplementedException();
    }

    private static string Replace(string source, string type, string value)
    {
        var start = source.IndexOf($"<{type}>", StringComparison.Ordinal);
        if (start < 0) throw new Exception($"The {type} not found in ovpn-conf.");
        start += 2 + type.Length;

        var end = source.IndexOf($"</{type}>", start + 2 + type.Length, StringComparison.Ordinal);
        if (end < 0) throw new Exception($"The {type} not found in ovpn-conf.");

        return new StringBuilder(source)
            .Remove(start, end - start)
            .Insert(start, value)
            .ToString();
    }

    [GeneratedRegex(@"\d+.+caller-id=([\.\d""]+) .+uptime=([\w""]*) .+session-id=([\w""]*)( |$)")]
    private static partial Regex ConnectionParse();
}