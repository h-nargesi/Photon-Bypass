using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.Test.Mock.MockFreeRadius.Model;

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
