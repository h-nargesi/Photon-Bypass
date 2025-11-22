using PhotonBypass.Domain.Profile;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class TopUpRepository(RadDbContext context) : DapperRepository<TopUpEntity>(context), ITopUpRepository
{
    public readonly static string TableName = EntityExtensions.GetTablename<TopUpEntity>();
    public readonly static string PermanentUserId = EntityExtensions.GetColumnName<TopUpEntity>(x => x.PermanentUserId);
    public readonly static string Id = EntityExtensions.GetColumnName<TopUpEntity>(x => x.Id);
    public readonly static string Data = EntityExtensions.GetColumnName<TopUpEntity>(x => x.Data);
    public readonly static string DaysToUse = EntityExtensions.GetColumnName<TopUpEntity>(x => x.DaysToUse);

    public async Task<TopUpEntity?> LatestOf(int user_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{PermanentUserId} = @user_id")
            .OrderBy($"{Id} desc")
            .WithParameters(new { user_id }));

        return result.FirstOrDefault();
    }
}
