using Dapper;
using Microsoft.Data.SqlClient;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class AccountRepository(LocalDbContext context) : EditableRepository<AccountEntity>(context), IAccountRepository
{
    public override async Task Save(AccountEntity entity)
    {
        try
        {
            await base.Save(entity);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new UserException(
                "این نام کاربری، ایمیل یا شماره همراه قبلا استفاده شده است!",
                $"Duplicate account data rejected by unique index: {ex.Message} (username={entity.Username})", 409);
        }
    }
    // Status predicates: "in (0, 1)" is the portal-accessible set — everything
    // but Deactivated (2); a VpnSuspended account still logs in and keeps its
    // target area (docs/20 D13). "= 2" marks Deactivated. The fragments are
    // inlined because Dapper.FastCrud renders non-property string arguments
    // as SQL parameters, not raw SQL.

    public async Task<List<AccountEntity>> GetAllActive()
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Status)} in (0, 1)"));

        return [.. result];
    }

    public async Task<AccountEntity?> GetActiveAccount(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Id)} = @id and {nameof(AccountEntity.Status)} in (0, 1)")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccount(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByMobile(string mobile)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Mobile)} = @mobile")
            .WithParameters(new { mobile }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByEmail(string email)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Email)} = @email")
            .WithParameters(new { email }));

        return result.FirstOrDefault();
    }

    public async Task<List<AccountEntity>> GetActiveTargetArea(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Owner)} = @account_id and {nameof(AccountEntity.Status)} in (0, 1)")
            .WithParameters(new { account_id }));

        return [.. result];
    }

    public async Task<Dictionary<int, AccountEntity>> GetActiveAccounts(IEnumerable<int> account_ids)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Id)} in @account_ids and {nameof(AccountEntity.Status)} in (0, 1)")
            .WithParameters(new { account_ids }));

        return result.ToDictionary(k => k.Id);
    }

    public Task UpdateWarningTimes(IEnumerable<int> account_ids)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(AccountEntity.LastWarningTime)} = @now
                   where {nameof(AccountEntity.Id)} in @account_ids
                   """;

        return ExecuteAsync(sql, new { account_ids, now = DateTime.Now });
    }

    public Task SetDeactivated(IEnumerable<int> account_ids)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(AccountEntity.Status)} = {(byte)AccountStatus.Deactivated}
                   where {nameof(AccountEntity.Id)} in @account_ids
                   """;

        return ExecuteAsync(sql, new { account_ids });
    }

    public async Task<Dictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames)
    {
        var sql = $"""
                   select {nameof(AccountEntity.Id)}, {nameof(AccountEntity.Username)}
                   from {TableName}
                   where {nameof(AccountEntity.Username)} in @usernames
                   """;

        var list = await QueryAsync(sql, new { usernames });

        return list.ToDictionary(pair => (string)pair.Username, pair => (int)pair.Id);
    }

    public async Task<Dictionary<int, string>> GetUsernamesByAccountId(IEnumerable<int> ids)
    {
        var sql = $"""
                   select {nameof(AccountEntity.Id)}, {nameof(AccountEntity.Username)}
                   from {TableName}
                   where {nameof(AccountEntity.Id)} in @ids
                   """;

        var list = await QueryAsync(sql, new { ids });

        return list.ToDictionary(pair => (int)pair.Id, pair => (string)pair.Username);
    }

    public async Task<int?> GetActiveAccountId(string username)
    {
        var result = await ExecuteScalarAsync<int?>(
            $"""
             select {nameof(AccountEntity.Id)} 
             from {TableName}
             where {nameof(AccountEntity.Username)} = @username and {nameof(AccountEntity.Status)} in (0, 1)
             """
            , new { username });

        return result;
    }

    public async Task<int> CheckUniqueData(string username, string? email, string? mobile)
    {
        var sql = $"""
                   select {nameof(AccountEntity.Username)}, {nameof(AccountEntity.Email)}, {nameof(AccountEntity.Mobile)}
                   from {TableName}
                   where {nameof(AccountEntity.Username)} = @username
                        {(email != null ? $"or {nameof(AccountEntity.Email)} = @email" : "")}
                        {(mobile != null ? $"or {nameof(AccountEntity.Mobile)} = @mobile" : "")}
                   """;

        var data = (await QueryAsync(sql, new { username, email, mobile })).ToList();

        if (data.Count < 1) return 0;

        return (data.Any(d => string.Equals(d.Username, username, StringComparison.OrdinalIgnoreCase)) ? 1 : 0) |
               (data.Any(d => string.Equals(d.Email, email, StringComparison.OrdinalIgnoreCase)) ? 2 : 0) |
               (data.Any(d => string.Equals(d.Mobile, mobile, StringComparison.OrdinalIgnoreCase)) ? 4 : 0);
    }

    public async Task<(List<AccountOverviewModel> Rows, int TotalCount)> SearchAccounts(int? owner_id, string? term, int page, int page_size)
    {
        var parameters = new DynamicParameters();
        var filters = new List<string>();

        if (owner_id.HasValue)
        {
            filters.Add($"ac.{nameof(AccountEntity.Owner)} = @owner_id");
            parameters.Add("owner_id", owner_id.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            filters.Add($"(ac.{nameof(AccountEntity.Username)} like @term or ac.{nameof(AccountEntity.Name)} like @term" +
                        $" or ac.{nameof(AccountEntity.Surname)} like @term or ac.{nameof(AccountEntity.Email)} like @term)");
            parameters.Add("term", $"%{term.Trim()}%");
        }

        var where = filters.Count > 0 ? "where " + string.Join(" and ", filters) : string.Empty;

        // the count never touches the TotalPlanState view — the plan join only
        // runs on the requested page (docs/20 phase 4)
        var total = await ExecuteScalarAsync<int>($"select count(*) from {TableName} ac {where}", parameters);

        var sql = $"""
                   select ac.{nameof(AccountEntity.Username)}
                          , ac.{nameof(AccountEntity.Name)}
                          , ac.{nameof(AccountEntity.Surname)}
                          , ac.{nameof(AccountEntity.Email)}
                          , ac.{nameof(AccountEntity.Role)}
                          , case when ac.{nameof(AccountEntity.UserType)} & 4 /* UserTypes.AllowSelfService */ <> 0 then 1 else 0 end as {nameof(AccountOverviewModel.AllowSelfService)}
                           , ac.{nameof(AccountEntity.Status)}
                          , owner.{nameof(AccountEntity.Username)} as {nameof(AccountOverviewModel.Owner)}
                          , ac.{nameof(AccountEntity.Created)}
                          , state.{nameof(AccountOverviewModel.SimultaneousUser)}
                          , state.{nameof(AccountOverviewModel.ExpirationDate)}
                          , state.{nameof(AccountOverviewModel.TrafficLeft)}
                          , state.{nameof(AccountOverviewModel.LastConnectTime)}
                   from {TableName} ac
                   left join {TableName} owner on ac.{nameof(AccountEntity.Owner)} = owner.{nameof(AccountEntity.Id)}
                   outer apply (select top 1 tps.SimultaneousUser, tps.ExpirationDate, tps.TrafficLeft, tps.LastConnectTime
                                from TotalPlanState tps
                                where tps.Id = ac.{nameof(AccountEntity.Id)}
                                order by tps.RowNumber desc) state
                   {where}
                   order by ac.{nameof(AccountEntity.Username)}
                   offset @skip rows fetch next @take rows only
                   """;

        parameters.Add("skip", (page - 1) * page_size);
        parameters.Add("take", page_size);

        var rows = await QueryAsync<AccountOverviewModel>(sql, parameters);

        return ([.. rows], total);
    }

    public Task<bool> HasActiveSubordinate(int manager_id)
    {
        var sql = $"""
                   select top 1 1
                   from {TableName}
                   where {nameof(AccountEntity.Owner)} = @manager_id and {nameof(AccountEntity.Status)} in (0, 1)
                   """;

        return ExecuteScalarAsync<bool>(sql, new { manager_id });
    }

    public Task SetTelegramChat(int account_id, long? chat_id)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(AccountEntity.TelegramChatId)} = @chat_id
                   where {nameof(AccountEntity.Id)} = @account_id
                   """;

        // a duplicate-key error from UK_Account_TelegramChatId (2601/2627)
        // propagates untouched: the caller (the bot link handler) turns it
        // into the "this chat already belongs to another account" reply
        return ExecuteAsync(sql, new { account_id, chat_id });
    }
}