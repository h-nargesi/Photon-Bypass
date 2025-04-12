using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra.Database.Local;

class LocalDapperOptions : IDapperOptions
{
    public string ConnectionString { get; set; } = null!;
}
