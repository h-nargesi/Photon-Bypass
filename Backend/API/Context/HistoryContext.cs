using System.Text.Json.Serialization;
using PhotonBypass.API.Basical;
using PhotonBypass.Tools;

namespace PhotonBypass.API.Context;

public class HistoryContext
{
    public string? Target { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? From { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? To { get; set; }
}
