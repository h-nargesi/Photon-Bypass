using PhotonBypass.Infra.Controller;
using System.Text.Json.Serialization;

namespace PhotonBypass.Domain.Model;

public class HistoryModel
{
    public int Id { get; set; }

    public string Target { get; set; } = string.Empty;

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime EventTime { get; set; }

    public string EventTimeTitle { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public object? Value { get; set; }

    public string? Unit { get; set; }

    public string? Description { get; set; }
}
