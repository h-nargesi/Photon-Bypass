using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class UserGroupRepository(LocalDbContext context) : EditableRepository<UserGroupEntity>(context), IUserGroupRepository
{
    public async Task<List<UserGroupEntity>> GetAll()
    {
        var result = await FindAsync(statement => statement
            .OrderBy($"{nameof(UserGroupEntity.Id)}"));

        return [.. result];
    }

    public async Task<UserGroupEntity?> Find(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(UserGroupEntity.Id)} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<UserGroupEntity?> FindByTitle(string title)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(UserGroupEntity.Title)} = @title")
            .WithParameters(new { title }));

        return result.FirstOrDefault();
    }

    public async Task<UserGroupEntity> GetDefault()
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(UserGroupEntity.IsDefault)} = 1"));

        return result.FirstOrDefault()
               ?? throw new Exception("The default user group («وب سایت») is missing — check the seed (Docs/24 D6).");
    }

    public Task<bool> HasMembers(int group_id)
    {
        const string sql = """
                           select top 1 1
                           from PanelUser
                           where PanelUser.UserGroupId = @group_id
                           """;

        return ExecuteScalarAsync<bool>(sql, new { group_id });
    }

    public async Task<Dictionary<int, int>> GetMemberCounts()
    {
        const string sql = """
                           select UserGroupId, count(*) as MemberCount
                           from PanelUser
                           group by UserGroupId
                           """;

        var rows = (await QueryAsync(sql)).ToList();

        return rows.ToDictionary(row => (int)row.UserGroupId, row => (int)row.MemberCount);
    }

    public Task ClearDefault(int except_id)
    {
        const string sql = """
                           update UserGroup
                           set UserGroup.IsDefault = 0
                           where UserGroup.IsDefault = 1 and UserGroup.Id <> @except_id
                           """;

        return ExecuteAsync(sql, new { except_id });
    }
}
