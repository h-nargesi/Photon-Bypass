namespace PhotonBypass.Infra.Database.Dapper;

public class LocalDapperOptions : IDapperOptions
{
    public string ConnectionString { get; set; } = null!;
}
