namespace PhotonBypass.Domain.Vpn;

public class TrafficDataEntity : IBaseEntity
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public DateTime Day {  get; set; }

    public long DataIn { get; set; }

    public long DataOut { get; set; }

    public long TotalData => DataIn + DataOut;
}
