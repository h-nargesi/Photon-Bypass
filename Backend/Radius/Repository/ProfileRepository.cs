using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius.Repository;

class ProfileRepository(RadDbContext context) : DapperRepository<ProfileEntity>(context), IProfileRepository
{
    public async Task<ProfileEntity> FindDefaultProfile(int cloud_id)
    {
        var result = await FindAsync(statement => statement
            .Where($@"{nameof(ProfileEntity.SimultaneousUse)} = 1
                  and {nameof(ProfileEntity.MikrotikRateLimit)} is not null
                  and {nameof(ProfileEntity.CloudId)} = @cloud_id")
            .WithParameters(new { cloud_id })
            .OrderBy($"{nameof(ProfileEntity.MikrotikRateLimit)}"));

        return result.First();
    }
}
