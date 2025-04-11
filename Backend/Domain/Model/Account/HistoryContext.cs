using System.Text.Json.Serialization;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain.Model.Account;

public class HistoryContext
{
    public string? Target { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? From { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime? To { get; set; }
}
