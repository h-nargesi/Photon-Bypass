namespace PhotonBypass.Domain.Static;

public class ConfigEntity : IBaseEntity
{
    public int Id { get; set; }

    public string Key { get; set; } = null!;

    public object? Value { get; set; }
}
