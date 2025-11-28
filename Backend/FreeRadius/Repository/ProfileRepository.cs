using PhotonBypass.Domain.Plan;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class ProfileRepository(RadDbContext context) : DapperRepository<ProfileEntity>(context), IProfileRepository
{
    private static readonly string SimultaneousUse = EntityExtensions.GetColumnName<ProfileEntity>(x => x.SimultaneousUse);
    private static readonly string MikrotikRateLimit = EntityExtensions.GetColumnName<ProfileEntity>(x => x.MikrotikRateLimit);
    private static readonly string CloudId = EntityExtensions.GetColumnName<ProfileEntity>(x => x.CloudId);
    private static readonly string PlanType = EntityExtensions.GetColumnName<ProfileEntity>(x => x.PlanType);

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
