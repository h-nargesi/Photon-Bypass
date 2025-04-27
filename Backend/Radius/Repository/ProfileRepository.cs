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
    readonly static string PlanType = EntityExtensions.GetColumnName<ProfileEntity>(x => x.PlanType);

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

    public async Task<ProfileEntity> GetProfile(int cloud_id, PlanType type, int count)
    {
        var result = await FindAsync(statement => statement
            .Where($@"{SimultaneousUse} = @count
                  and {PlanType} = @type
                  and {MikrotikRateLimit} is not null
                  and {CloudId} = @cloud_id")
            .WithParameters(new { cloud_id, count, type })
            .OrderBy($"{MikrotikRateLimit}"));

        return result.First();
    }
}
