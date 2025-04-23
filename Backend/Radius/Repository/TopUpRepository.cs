using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

class TopUpRepository(RadDbContext context) : DapperRepository<TopUpEntity>(context), ITopUpRepository
{
    readonly static string PermanentUserId = EntityExtensions.GetColumnName<TopUpEntity>(x => x.PermanentUserId);
    readonly static string Created = EntityExtensions.GetColumnName<TopUpEntity>(x => x.Created);

    public async Task<TopUpEntity?> LatestOf(int user_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{PermanentUserId} = @user_id")
            .OrderBy($"{Created} desc")
            .WithParameters(new { user_id }));

        return result.FirstOrDefault();
    }
}
