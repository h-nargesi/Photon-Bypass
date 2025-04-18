using PhotonBypass.Infra.Database;

namespace PhotonBypass.Radius.Repository.DbContext;

class RadDapperOptions : IDapperOptions
{
    public string ConnectionString { get; set; } = null!;
}
