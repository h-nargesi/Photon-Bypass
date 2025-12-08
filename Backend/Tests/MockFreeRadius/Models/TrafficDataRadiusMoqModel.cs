using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.Test.MockOutSources.Models;
using System.Text.Json.Serialization;

namespace PhotonBypass.Test.MockFreeRadius.Models;

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
