namespace PhotonBypass.Application.Vpn.Model;

public class TrafficDataModel
{
    public string Title { get; set; } = null!;

    public TrafficRecordModel[] Collections { get; set; } = null!;

    public string[] Labels { get; set; } = null!;
}

public class TrafficRecordModel
{
    public string Title { get; set; } = null!;

    public int[] Data { get; set; } = null!;
}