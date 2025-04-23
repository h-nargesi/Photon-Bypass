using Dapper.FastCrud;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

class PermanentUsersRepository(RadDbContext context) : DapperRepository<PermanentUserEntity>(context), IPermanentUsersRepository
{
    readonly static string Id = EntityExtensions.GetColumnName<PermanentUserEntity>(x => x.Id);
    readonly static string Username = EntityExtensions.GetColumnName<PermanentUserEntity>(x => x.Username);

    readonly static string StateId = EntityExtensions.GetColumnName<UserPlanStateEntity>(x => x.Id);
    readonly static string StateUsername = EntityExtensions.GetColumnName<UserPlanStateEntity>(x => x.Username);

    public async Task<PermanentUserEntity?> GetUser(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{Id} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<PermanentUserEntity?> GetUser(string username)
    {
        var result = await FindUserAsync(username);

        return result.FirstOrDefault();
    }

    public async Task<bool> CheckUsername(string username)
    {
        var result = await FindUserAsync(username);

        return result.Any();
    }

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
        var result = await connection.FindAsync<UserPlanStateEntity>(statement => statement
            .Where($"{StateUsername} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    private Task<IEnumerable<UserPlanStateEntity>> FindStateAsync(int id)
    {
        return connection.FindAsync<UserPlanStateEntity>(statement => statement
            .Where($"{StateId} = @id")
            .WithParameters(new { id }));
    }

    private Task<IEnumerable<PermanentUserEntity>> FindUserAsync(string username)
    {
        return FindAsync(statement => statement
            .Where($"{Username} = @username")
            .WithParameters(new { username }));
    }
}
