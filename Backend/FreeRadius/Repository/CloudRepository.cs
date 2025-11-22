using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class CloudRepository(RadDbContext context) : DapperRepository<CloudEntity>(context), ICloudRepository
{
    readonly static string Name = EntityExtensions.GetColumnName<CloudEntity>(x => x.Name);

    public async Task<int> FindWebCloud()
    {
        var result = await FindAsync(statement => statement
            .Where($"{Name} = 'Web'"));

        var cloud = result.FirstOrDefault();

        return cloud?.Id ?? -1;
    }
}
