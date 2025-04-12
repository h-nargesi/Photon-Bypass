using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra.Database.Local;

class LocalDbContext(IOptions<LocalDapperOptions> options) : DapperDbContext(options)
{
}
