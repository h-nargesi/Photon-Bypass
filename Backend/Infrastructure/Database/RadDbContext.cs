using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra.Database;

class RadDbContext(IOptions<RadDapperOptions> options) : DapperDbContext(options)
{
}
