using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra.Database.Radius;

class RadDapperOptions : IDapperOptions
{
    public string ConnectionString { get; set; } = null!;
}
