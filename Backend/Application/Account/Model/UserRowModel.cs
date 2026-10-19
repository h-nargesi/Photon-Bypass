using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Tools;
using System.Text.Json.Serialization;

namespace PhotonBypass.Application.Account.Model;

/// <summary>API row of the management user list (docs/20 phase 4).</summary>
public class UserRowModel
{
    public string Username { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    public AccountRole Role { get; set; }

    public bool AllowSelfService { get; set; }

    public AccountStatus Status { get; set; }

    public string? Owner { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime Created { get; set; }

    public string CreatedTitle { get; set; } = null!;

    // Latest plan summary (null when the account never renewed)

    public byte? SimultaneousUser { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? ExpirationDate { get; set; }

    public long? TrafficLeft { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? LastConnectTime { get; set; }
}
