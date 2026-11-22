using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IUserGroupRepository : IEditableRepository<UserGroupEntity>
{
    Task<List<UserGroupEntity>> GetAll();

    Task<UserGroupEntity?> Find(int id);

    /// <summary>Group lookup by title (duplicate guard).</summary>
    Task<UserGroupEntity?> FindByTitle(string title);

    /// <summary>The «وب سایت» group — the fallback of every creation path (Docs/24 D6).</summary>
    Task<UserGroupEntity> GetDefault();

    /// <summary>Whether any member still references the group (delete guard).</summary>
    Task<bool> HasMembers(int group_id);

    /// <summary>Member counts per group id (groups with zero members omitted) — the admin list.</summary>
    Task<Dictionary<int, int>> GetMemberCounts();

    /// <summary>Unsets <c>IsDefault</c> on every group except <paramref name="except_id"/> (default-move).</summary>
    Task ClearDefault(int except_id);
}
