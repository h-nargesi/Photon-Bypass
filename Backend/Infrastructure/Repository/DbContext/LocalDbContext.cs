using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Infra.Repository.DbContext;

class LocalDbContext(IOptions<LocalDapperOptions> options) : DapperDbContext(options)
{
}
