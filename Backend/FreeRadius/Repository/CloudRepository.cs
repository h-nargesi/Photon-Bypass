using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

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
