using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// Turns the stored server row into an <see cref="SshEndpoint"/> and derives
/// the fingerprint the session pool compares a live session against.
/// </summary>
internal static class SshEndpointFactory
{
    public static SshEndpoint Create(
        ServerEntity server, ServerConfiguration configuration, HostKeyPolicy policy, ServerBridgeOptions options)
    {
        var ssh = configuration.SshConfig ??
                  throw new InvalidOperationException($"The ssh configuration is not set for server ({server.Id}:{server.Name})");

        if (policy == HostKeyPolicy.Required)
        {
            // rows that reached the database without the admin validation (raw SQL) still fail with the admin wording
            if (string.IsNullOrWhiteSpace(ssh.Username))
            {
                throw new UserException("برای حالت تانل، کاربر SSH الزامی است!",
                    $"SshTunnel transport requires SshConfig.Username on server ({server.Id}:{server.Name})");
            }

            if (string.IsNullOrWhiteSpace(ssh.PrivateKey))
            {
                throw new UserException("کلید خصوصی SSH معتبر نیست!",
                    $"SshTunnel transport requires SshConfig.PrivateKey on server ({server.Id}:{server.Name})");
            }
        }

        var pin = SshTunnelConfig.NormalizeHostKeyPin(ssh.HostKeySha256);

        return new SshEndpoint(
            Host: server.IpAddress,
            Port: ssh.Port,
            Username: ssh.Username ?? string.Empty,
            Password: string.IsNullOrEmpty(ssh.Password) ? null : ssh.Password,
            PrivateKey: string.IsNullOrWhiteSpace(ssh.PrivateKey) ? null : ssh.PrivateKey,
            PassPhrase: string.IsNullOrEmpty(ssh.PassPhrase) ? null : ssh.PassPhrase,
            HostKeySha256: pin.Length < 1 ? null : pin,
            HostKeyPolicy: policy,
            ConnectTimeout: TimeSpan.FromSeconds(options.SshConnectTimeoutSeconds),
            KeepAlive: TimeSpan.FromSeconds(options.SshKeepAliveIntervalSeconds))
        {
            Fingerprint = BuildFingerprint(server.IpAddress, ssh, pin),
        };
    }

    private static string BuildFingerprint(string host, SshConfig ssh, string pin)
    {
        var key_hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ssh.PrivateKey ?? string.Empty)));

        return $"{host}|{ssh.Port}|{ssh.Username}|{pin}|{key_hash}";
    }
}
