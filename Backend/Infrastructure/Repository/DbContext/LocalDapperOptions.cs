using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Repository.DbContext;

class LocalDapperOptions : IDapperOptions
{
    public string ConnectionString { get; set; } = null!;
}
