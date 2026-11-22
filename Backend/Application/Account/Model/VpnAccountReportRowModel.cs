using System.Text.Json.Serialization;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Tools;

namespace PhotonBypass.Application.Account.Model;

/// <summary>
/// One row of the flat VPN-account report (Docs/24 §5). NULL axis values mean
/// unlimited — the UI renders them as «نامحدود» and sorts them last.
/// </summary>
public class VpnAccountReportRowModel
{
    public int AccountId { get; set; }

    public string Username { get; set; } = null!;

    public string OwnerUsername { get; set; } = null!;

    public string OwnerFullname { get; set; } = null!;

    public int RealmId { get; set; }

    public string? RealmName { get; set; }

    public VpnAccountStatus Status { get; set; }

    public byte? SimultaneousUser { get; set; }

    public short? TimeLimitInDays { get; set; }

    /// <summary>Bytes; null = unlimited.</summary>
    public long? TrafficLimit { get; set; }

    public int? RateLimitInMeg { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Bytes; null = unlimited/no quota.</summary>
    public long? TrafficLeft { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? LastConnectTime { get; set; }

    public string? GroupTitle { get; set; }
}
