using Dapper;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

class UserPlanStateRepository(RadDbContext context) : DapperRepository<UserPlanStateEntity>(context), IUserPlanStateRepository
{
    readonly static string TableName = EntityExtensions.GetTablename<UserPlanStateEntity>();
    readonly static string Id = EntityExtensions.GetColumnName<UserPlanStateEntity>(x => x.Id);
    readonly static string Username = EntityExtensions.GetColumnName<UserPlanStateEntity>(x => x.Username);
    readonly static string LeftDays = EntityExtensions.GetColumnName<UserPlanStateEntity>(x => x.LeftDays);
    readonly static string AccountDisabled = EntityExtensions.GetColumnName<UserPlanStateEntity>(x => x.AccountDisabled);
    readonly static string GigaLeft = EntityExtensions.GetColumnName<UserPlanStateEntity>(x => x.GigaLeft);

    public async Task<string?> GetRestrictedServer(int id)
    {
        var result = await FindStateAsync(id);

        return result.FirstOrDefault()?.RestrictedServerIP;
    }

    public async Task<UserPlanStateEntity?> GetPlanState(int id)
    {
        var result = await FindStateAsync(id);

        return result.FirstOrDefault();
    }

    public async Task<UserPlanStateEntity?> GetPlanState(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{Username} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    private Task<IEnumerable<UserPlanStateEntity>> FindStateAsync(int id)
    {
        return FindAsync(statement => statement
            .Where($"{Id} = @id")
            .WithParameters(new { id }));
    }

    public async Task<IList<UserPlanStateEntity>> GetMonthlyPlanOverState(int days)
    {
        var result = await FindAsync(statement => statement
            .Where($"{LeftDays} = @days")
            .WithParameters(new { days }));

        return [.. result];
    }

    public async Task<IList<UserPlanStateEntity>> GetPlanOverState(float percent)
    {
        var query = @$"
select *
from {TableName} u
where {AccountDisabled} = 0 and exists (
    select *
    from (
        select *
        from {TopUpRepository.TableName} t
        where t.{TopUpRepository.PermanentUserId} = u.{Id}
        order by t.{TopUpRepository.Id} desc
        limit 1
    ) t
    where t.{TopUpRepository.DaysToUse} is not null and (@percent * t.{TopUpRepository.DaysToUse}) > u.{LeftDays}
       or t.{TopUpRepository.Data} is not null and (@percent * to_gigabyte(t.{TopUpRepository.Data})) > u.{GigaLeft}
)
        ";

        var result = await connection.QueryAsync<UserPlanStateEntity>(query, new { percent });

        return [.. result];
    }
}
