using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

class ProfileRepository(RadDbContext context) : DapperRepository<ProfileEntity>(context), IProfileRepository
{
    readonly static string SimultaneousUse = EntityExtensions.GetColumnName<ProfileEntity>(x => x.SimultaneousUse);
    readonly static string MikrotikRateLimit = EntityExtensions.GetColumnName<ProfileEntity>(x => x.MikrotikRateLimit);
    readonly static string CloudId = EntityExtensions.GetColumnName<ProfileEntity>(x => x.CloudId);

    public async Task<ProfileEntity> FindDefaultProfile(int cloud_id)
    {
        var result = await FindAsync(statement => statement
            .Where($@"{SimultaneousUse} = 1
                  and {MikrotikRateLimit} is not null
                  and {CloudId} = @cloud_id")
            .WithParameters(new { cloud_id })
            .OrderBy($"{MikrotikRateLimit}"));

        return result.First();
    }
}
