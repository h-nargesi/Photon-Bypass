using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain;

public class HistoryEntity : IBaseEntity
{
    public int Id { get; set; }

    public string Target { get; set; } = null!;

    public DateTime EventTime { get; set; }

    public string Title { get; set; } = null!;

    public string Color { get; set; } = null!;

    public object? Value { get; set; }

    public string? Unit { get; set; }

    public string? Description { get; set; }
}
