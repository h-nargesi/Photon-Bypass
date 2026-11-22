using Dapper;
using Microsoft.Data.SqlClient;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PanelUserRepository(LocalDbContext context) : EditableRepository<PanelUserEntity>(context), IPanelUserRepository
{
    public override async Task Save(PanelUserEntity entity)
    {
        try
        {
            await base.Save(entity);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new UserException(
                "این نام کاربری، ایمیل یا شماره همراه قبلا استفاده شده است!",
                $"Duplicate panel-user data rejected by unique index: {ex.Message} (username={entity.Username})", 409);
        }
    }

    // Status predicate: PanelUser has exactly two states (Docs/22 D12) —
    // "= 2" marks Deactivated and everything else is portal-accessible, so
    // the fragments below are plain status equality. They are inlined because
    // Dapper.FastCrud renders non-property string arguments as SQL parameters,
    // not raw SQL.

    public async Task<List<PanelUserEntity>> GetAllActive()
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PanelUserEntity.Status)} <> {(byte)PanelUserStatus.Deactivated}"));

        return [.. result];
    }

    public async Task<PanelUserEntity?> GetActiveUser(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PanelUserEntity.Id)} = @id and {nameof(PanelUserEntity.Status)} <> {(byte)PanelUserStatus.Deactivated}")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<PanelUserEntity?> GetUser(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PanelUserEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<PanelUserEntity?> GetUserByMobile(string mobile)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PanelUserEntity.Mobile)} = @mobile")
            .WithParameters(new { mobile }));

        return result.FirstOrDefault();
    }

    public async Task<PanelUserEntity?> GetUserByEmail(string email)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PanelUserEntity.Email)} = @email")
            .WithParameters(new { email }));

        return result.FirstOrDefault();
    }

    public async Task<List<PanelUserEntity>> GetActiveTargetArea(int user_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PanelUserEntity.Owner)} = @user_id and {nameof(PanelUserEntity.Status)} <> {(byte)PanelUserStatus.Deactivated}")
            .WithParameters(new { user_id }));

        return [.. result];
    }

    public async Task<Dictionary<int, PanelUserEntity>> GetActiveUsers(IEnumerable<int> user_ids)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PanelUserEntity.Id)} in @user_ids and {nameof(PanelUserEntity.Status)} <> {(byte)PanelUserStatus.Deactivated}")
            .WithParameters(new { user_ids }));

        return result.ToDictionary(k => k.Id);
    }

    public Task SetDeactivated(IEnumerable<int> user_ids)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(PanelUserEntity.Status)} = {(byte)PanelUserStatus.Deactivated}
                   where {nameof(PanelUserEntity.Id)} in @user_ids
                   """;

        return ExecuteAsync(sql, new { user_ids });
    }

    public async Task<Dictionary<int, string>> GetUsernamesByUserId(IEnumerable<int> ids)
    {
        var sql = $"""
                   select {nameof(PanelUserEntity.Id)}, {nameof(PanelUserEntity.Username)}
                   from {TableName}
                   where {nameof(PanelUserEntity.Id)} in @ids
                   """;

        var list = await QueryAsync(sql, new { ids });

        return list.ToDictionary(pair => (int)pair.Id, pair => (string)pair.Username);
    }

    public async Task<int?> GetActiveUserId(string username)
    {
        return await ExecuteScalarAsync<int?>(
            $"""
             select {nameof(PanelUserEntity.Id)}
             from {TableName}
             where {nameof(PanelUserEntity.Username)} = @username and {nameof(PanelUserEntity.Status)} <> {(byte)PanelUserStatus.Deactivated}
             """
            , new { username });
    }

    public async Task<int> CheckUniqueData(string username, string? email, string? mobile)
    {
        var sql = $"""
                   select {nameof(PanelUserEntity.Username)}, {nameof(PanelUserEntity.Email)}, {nameof(PanelUserEntity.Mobile)}
                   from {TableName}
                   where {nameof(PanelUserEntity.Username)} = @username
                        {(email != null ? $"or {nameof(PanelUserEntity.Email)} = @email" : "")}
                        {(mobile != null ? $"or {nameof(PanelUserEntity.Mobile)} = @mobile" : "")}
                   """;

        var data = (await QueryAsync(sql, new { username, email, mobile })).ToList();

        if (data.Count < 1) return 0;

        return (data.Any(d => string.Equals(d.Username, username, StringComparison.OrdinalIgnoreCase)) ? 1 : 0) |
               (data.Any(d => string.Equals(d.Email, email, StringComparison.OrdinalIgnoreCase)) ? 2 : 0) |
               (data.Any(d => string.Equals(d.Mobile, mobile, StringComparison.OrdinalIgnoreCase)) ? 4 : 0);
    }

    public async Task<(List<UserOverviewModel> Rows, int TotalCount)> SearchUsers(int? owner_id, string? term, int page, int page_size)
    {
        var parameters = new DynamicParameters();
        var filters = new List<string>();

        if (owner_id.HasValue)
        {
            filters.Add($"ac.{nameof(PanelUserEntity.Owner)} = @owner_id");
            parameters.Add("owner_id", owner_id.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            filters.Add($"(ac.{nameof(PanelUserEntity.Username)} like @term or ac.{nameof(PanelUserEntity.Name)} like @term" +
                        $" or ac.{nameof(PanelUserEntity.Surname)} like @term or ac.{nameof(PanelUserEntity.Email)} like @term)");
            parameters.Add("term", $"%{term.Trim()}%");
        }

        var where = filters.Count > 0 ? "where " + string.Join(" and ", filters) : string.Empty;

        // the count never touches the plan views — the account join only
        // runs on the requested page (Docs/20 phase 4, Docs/22 §3.6)
        var total = await ExecuteScalarAsync<int>($"select count(*) from {TableName} ac {where}", parameters);

        var sql = $"""
                   select ac.{nameof(PanelUserEntity.Username)}
                          , ac.{nameof(PanelUserEntity.Name)}
                          , ac.{nameof(PanelUserEntity.Surname)}
                          , ac.{nameof(PanelUserEntity.Email)}
                          , ac.{nameof(PanelUserEntity.Role)}
                          , case when ac.{nameof(PanelUserEntity.UserType)} & 4 /* UserTypes.AllowSelfService */ <> 0 then 1 else 0 end as {nameof(UserOverviewModel.AllowSelfService)}
                           , ac.{nameof(PanelUserEntity.Status)}
                          , owner.{nameof(PanelUserEntity.Username)} as {nameof(UserOverviewModel.Owner)}
                          , grp.{nameof(UserGroupEntity.Title)} as {nameof(UserOverviewModel.GroupTitle)}
                          , ac.{nameof(PanelUserEntity.Created)}
                   from {TableName} ac
                   left join {TableName} owner on ac.{nameof(PanelUserEntity.Owner)} = owner.{nameof(PanelUserEntity.Id)}
                   left join {UserGroupRepository.TableName} grp on ac.{nameof(PanelUserEntity.UserGroupId)} = grp.{nameof(UserGroupEntity.Id)}
                   {where}
                   order by ac.{nameof(PanelUserEntity.Username)}
                   offset @skip rows fetch next @take rows only
                   """;

        parameters.Add("skip", (page - 1) * page_size);
        parameters.Add("take", page_size);

        var rows = await QueryAsync<UserOverviewModel>(sql, parameters);

        return ([.. rows], total);
    }

    public Task<bool> HasActiveSubordinate(int manager_id)
    {
        var sql = $"""
                   select top 1 1
                   from {TableName}
                   where {nameof(PanelUserEntity.Owner)} = @manager_id and {nameof(PanelUserEntity.Status)} <> {(byte)PanelUserStatus.Deactivated}
                   """;

        return ExecuteScalarAsync<bool>(sql, new { manager_id });
    }

    public Task SetTelegramChat(int user_id, long? chat_id)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(PanelUserEntity.TelegramChatId)} = @chat_id
                   where {nameof(PanelUserEntity.Id)} = @user_id
                   """;

        // a duplicate-key error from UK_PanelUser_TelegramChatId (2601/2627)
        // propagates untouched: the caller (the bot link handler) turns it
        // into the "this chat already belongs to another account" reply
        return ExecuteAsync(sql, new { user_id, chat_id });
    }
}
