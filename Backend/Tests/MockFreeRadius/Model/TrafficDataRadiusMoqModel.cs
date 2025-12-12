using System.Text.Json.Serialization;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.Test.MockOptions;

namespace PhotonBypass.Test.MockFreeRadius.Model;

class TrafficDataRadiusMoqModel
{
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
