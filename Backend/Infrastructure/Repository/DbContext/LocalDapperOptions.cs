using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Repository.DbContext;

class LocalDapperOptions
{
    public string ConnectionString { get; set; } = null!;
}
