namespace PhotonBypass.Infra.Dto;

public class TrafficDataDto
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string? NasName { get; set; }

    public string? NasIpAddress { get; set; }

    public string SessionId { get; set; } = null!;

    public DateTime StartSession { get; set; }

    public DateTime? EndSession { get; set; }

    public long DataIn { get; set; }

    public long DataOut { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;
}
