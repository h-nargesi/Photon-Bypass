using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Repository.DbContext;

public class LocalDapperOptions
{
    public string ConnectionString { get; init; } = null!;
}
