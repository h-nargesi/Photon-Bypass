namespace PhotonBypass.Infra.Database.Dapper;

class RadDapperOptions : IDapperOptions
{
    public string ConnectionString { get; set; } = null!;
}
