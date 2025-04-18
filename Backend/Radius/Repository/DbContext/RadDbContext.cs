using Microsoft.Extensions.Options;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Radius.Repository.DbContext;

class RadDbContext(IOptions<RadDapperOptions> options) : DapperDbContext(options)
{
}
