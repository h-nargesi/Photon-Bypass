using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Tools;
using System.Text.Json.Serialization;

namespace PhotonBypass.Application.Account.Model;

/// <summary>API row of the management user list (Docs/20 phase 4, Docs/22 §3.6).</summary>
public class UserRowModel
{
    public string Username { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    public AccountRole Role { get; set; }

    public bool AllowSelfService { get; set; }

    public PanelUserStatus Status { get; set; }

    public string? Owner { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime Created { get; set; }

    public string CreatedTitle { get; set; } = null!;

    /// <summary>VPN accounts of the user (Docs/22 D2), nested under the row.</summary>
    public IList<UserAccountRowModel> Accounts { get; set; } = [];
}

/// <summary>One VPN account nested under a management user row (Docs/22 D12).</summary>
public class UserAccountRowModel
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public int RealmId { get; set; }

    public string? RealmName { get; set; }

    public VpnAccountStatus Status { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime Created { get; set; }

    // Latest plan summary (null when the account never renewed)

    public byte? SimultaneousUser { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? ExpirationDate { get; set; }

    public long? TrafficLeft { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? LastConnectTime { get; set; }
}
