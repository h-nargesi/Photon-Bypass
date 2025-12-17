namespace PhotonBypass.Domain.Plan.Model;

public class TrafficDataBinding
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string NasIpAddress { get; set; } = null!;

    public string SessionId { get; set; } = null!;

    public DateTime StartSession { get; set; }

    public DateTime? EndSession { get; set; }

    public long DataIn { get; set; }

    public long DataOut { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}
