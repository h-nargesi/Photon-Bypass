using PhotonBypass.Domain.Vpn;
using System.Text.Json.Serialization;

namespace PhotonBypass.Test.MockOutSources.Models;

class TrafficDataRadiusMoqModel
{
    [JsonConverter(typeof(DateTimeJsonConverter))]
    public DateTime Day { get; set; }

    public long DataIn { get; set; }

    public long DataOut { get; set; }

    public TrafficDataRadius ToEntity()
    {
        return new TrafficDataRadius
        {
            Day = Day,
            DataIn = DataIn,
            DataOut = DataOut,
        };
    }
}
