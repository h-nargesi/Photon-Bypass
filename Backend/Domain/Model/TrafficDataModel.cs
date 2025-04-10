namespace PhotonBypass.Domain.Model;

public class TrafficDataModel
{
    public string Title { get; set; } = string.Empty;

    public TrafficRecordModel[] Collections { get; set; } = [];

    public string[] Labels { get; set; } = [];
}

public class TrafficRecordModel
{
    public string Title { get; set; } = string.Empty;

    public int[] Data { get; set; } = [];
}