using System.Text.Json.Serialization;
using PhotonBypass.Tools;

namespace PhotonBypass.API.Context;

public class HistoryContext
{
    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? From { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? To { get; set; }
}
