using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Plan.Model;

/// <summary>
/// One row of the flat VPN-account report (Docs/24 §5): the latest plan band
/// of every account joined with owner, realm and pricing group. NULL axis
/// values mean unlimited; UIs sort them as the largest value.
/// </summary>
public class VpnAccountReportRow
{
    public int AccountId { get; set; }

    public string Username { get; set; } = null!;

    public string OwnerUsername { get; set; } = null!;

    public string? OwnerName { get; set; }

    public string? OwnerSurname { get; set; }

    public int RealmId { get; set; }

    public string? RealmName { get; set; }

    /// <summary>Docs/24 D8: user-facing realm title (falls back to <see cref="RealmName"/>).</summary>
    public string? RealmDisplayName { get; set; }

    public VpnAccountStatus Status { get; set; }

    public byte? SimultaneousUser { get; set; }

    public short? TimeLimitInDays { get; set; }

    /// <summary>Bytes; null = unlimited.</summary>
    public long? TrafficLimit { get; set; }

    public int? RateLimitInMeg { get; set; }

    public DateTime? ExpirationDate { get; set; }

    /// <summary>Bytes; null = unlimited/no quota.</summary>
    public long? TrafficLeft { get; set; }

    public DateTime? LastConnectTime { get; set; }

    public string? GroupTitle { get; set; }
}
