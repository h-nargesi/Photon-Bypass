using PhotonBypass.Domain.Plan;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class TopUpRepository(RadDbContext context) : DapperRepository<TopUpEntity>(context), ITopUpRepository
{
    public static readonly string PermanentUserId = EntityExtensions.GetColumnName<TopUpEntity>(x => x.PermanentUserId);
    public static readonly string Data = EntityExtensions.GetColumnName<TopUpEntity>(x => x.Data);
    public static readonly string DaysToUse = EntityExtensions.GetColumnName<TopUpEntity>(x => x.DaysToUse);

    public async Task<TopUpEntity?> LatestOf(int user_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{PermanentUserId} = @user_id")
            .OrderBy($"{Id} desc")
            .WithParameters(new { user_id }));

        return result.FirstOrDefault();
    }
}
