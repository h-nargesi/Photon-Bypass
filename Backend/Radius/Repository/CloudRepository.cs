using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius.Repository;

class CloudRepository(RadDbContext context) : DapperRepository<CloudEntity>(context), ICloudRepository
{
    public async Task<int> FindWebCloud()
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(CloudEntity.Name)} = 'Web'"));

        var cloud = result.FirstOrDefault();

        return cloud?.Id ?? -1;
    }
}
