namespace PhotonBypass.RadiusDesk.Entity;

public class TrafficDataRadius
{
    public DateTime Day {  get; set; }

    public long DataIn { get; set; }

    public long DataOut { get; set; }

    public long TotalData => DataIn + DataOut;
}
