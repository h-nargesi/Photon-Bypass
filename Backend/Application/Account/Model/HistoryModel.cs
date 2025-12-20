using PhotonBypass.Tools;
using System.Text.Json.Serialization;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Application.Account.Model;

public class HistoryModel
{
    public int Id { get; set; }

    public string? Issuer { get; set; }

    public string Target { get; set; } = null!;

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime EventTime { get; set; }

    public string EventTimeTitle { get; set; } = null!;

    public string Title { get; set; } = null!;

    public EventCategory Category { get; init; }

    public EventType Type { get; init; }

    public string? Value { get; set; }

    public int? Price { get; init; }

    public string? Description { get; set; }
}