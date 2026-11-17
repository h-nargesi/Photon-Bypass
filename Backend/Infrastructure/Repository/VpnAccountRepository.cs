using Dapper;
using Microsoft.Data.SqlClient;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class VpnAccountRepository(LocalDbContext context) : EditableRepository<VpnAccountEntity>(context), IVpnAccountRepository
{
    public override async Task Save(VpnAccountEntity entity)
    {
        try
        {
            await base.Save(entity);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // the unique index closes the suggest/submit TOCTOU window of D3
            throw new UserException(
                "این نام کاربری VPN قبلا استفاده شده است!",
                $"Duplicate VPN username rejected by unique index: {ex.Message} (username={entity.Username})", 409);
        }
    }

    public async Task<List<VpnAccountEntity>> GetByUser(int user_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(VpnAccountEntity.UserId)} = @user_id")
            .WithParameters(new { user_id }));

        return [.. result];
    }

    public async Task<List<VpnAccountEntity>> GetVisibleByUser(int user_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(VpnAccountEntity.UserId)} = @user_id and {nameof(VpnAccountEntity.Status)} <> {(byte)VpnAccountStatus.Deactivated}")
            .WithParameters(new { user_id }));

        return [.. result];
    }

    public async Task<List<VpnAccountEntity>> GetVisibleByUsers(IEnumerable<int> user_ids)
    {
        var id_list = user_ids.ToList();

        if (id_list.Count == 0) return [];

        var result = await FindAsync(statement => statement
            .Where($"{nameof(VpnAccountEntity.UserId)} in @user_ids and {nameof(VpnAccountEntity.Status)} <> {(byte)VpnAccountStatus.Deactivated}")
            .WithParameters(new { user_ids = id_list }));

        return [.. result];
    }

    public async Task<VpnAccountEntity?> GetAccount(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(VpnAccountEntity.Id)} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<VpnAccountEntity?> GetVisibleAccount(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(VpnAccountEntity.Id)} = @id and {nameof(VpnAccountEntity.Status)} <> {(byte)VpnAccountStatus.Deactivated}")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<VpnAccountEntity?> GetAccount(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(VpnAccountEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<Dictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames)
    {
        var sql = $"""
                   select {nameof(VpnAccountEntity.Id)}, {nameof(VpnAccountEntity.Username)}
                   from {TableName}
                   where {nameof(VpnAccountEntity.Username)} in @usernames
                   """;

        var list = await QueryAsync(sql, new { usernames });

        return list.ToDictionary(pair => (string)pair.Username, pair => (int)pair.Id);
    }

    public async Task<Dictionary<int, string>> GetUsernamesByAccountId(IEnumerable<int> ids)
    {
        var sql = $"""
                   select {nameof(VpnAccountEntity.Id)}, {nameof(VpnAccountEntity.Username)}
                   from {TableName}
                   where {nameof(VpnAccountEntity.Id)} in @ids
                   """;

        var list = await QueryAsync(sql, new { ids });

        return list.ToDictionary(pair => (int)pair.Id, pair => (string)pair.Username);
    }

    public async Task<Dictionary<int, VpnAccountEntity>> GetActiveAccounts(IEnumerable<int> account_ids)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(VpnAccountEntity.Id)} in @account_ids and {nameof(VpnAccountEntity.Status)} = {(byte)VpnAccountStatus.Active}")
            .WithParameters(new { account_ids }));

        return result.ToDictionary(k => k.Id);
    }

    public async Task<List<VpnAccountEntity>> GetAll()
    {
        return [.. await FindAsync()];
    }

    public async Task<List<UserAccountOverviewModel>> GetAccountsOverview(IEnumerable<string> panel_usernames)
    {
        var usernames = panel_usernames.ToList();

        if (usernames.Count == 0) return [];

        var sql = $"""
                   select pu.{nameof(PanelUserEntity.Username)} as {nameof(UserAccountOverviewModel.Owner)}
                          , va.{nameof(VpnAccountEntity.UserId)}
                          , va.{nameof(VpnAccountEntity.Id)} as {nameof(UserAccountOverviewModel.AccountId)}
                          , va.{nameof(VpnAccountEntity.Username)}
                          , va.{nameof(VpnAccountEntity.RealmId)}
                          , realm.{nameof(RealmEntity.Name)} as {nameof(UserAccountOverviewModel.RealmName)}
                          , va.{nameof(VpnAccountEntity.Status)}
                          , va.{nameof(VpnAccountEntity.Created)}
                          , state.{nameof(UserAccountOverviewModel.SimultaneousUser)}
                          , state.{nameof(UserAccountOverviewModel.ExpirationDate)}
                          , state.{nameof(UserAccountOverviewModel.TrafficLeft)}
                          , state.{nameof(UserAccountOverviewModel.LastConnectTime)}
                   from {TableName} va
                   join {PanelUserRepository.TableName} pu on va.{nameof(VpnAccountEntity.UserId)} = pu.{nameof(PanelUserEntity.Id)}
                   left join {RealmRepository.TableName} realm on va.{nameof(VpnAccountEntity.RealmId)} = realm.{nameof(RealmEntity.Id)}
                   outer apply (select top 1 tps.SimultaneousUser, tps.ExpirationDate, tps.TrafficLeft, tps.LastConnectTime
                                from TotalPlanState tps
                                where tps.Id = va.{nameof(VpnAccountEntity.Id)}
                                order by tps.RowNumber desc) state
                   where pu.{nameof(PanelUserEntity.Username)} in @usernames
                   order by va.{nameof(VpnAccountEntity.Id)}
                   """;

        var rows = await QueryAsync<UserAccountOverviewModel>(sql, new { usernames });

        return [.. rows];
    }

    public async Task<string> SuggestUsername(string panel_username)
    {
        foreach (var candidate in AccountBusiness.SuggestVpnUsernameSequence(panel_username))
        {
            if (await GetAccount(candidate) == null)
            {
                return candidate;
            }
        }

        throw new UserException("ظرفیت نام کاربری برای این کاربر تکمیل است!",
            $"No free VPN username suggestion within the length cap (Docs/22 D3): owner={panel_username}");
    }

    public Task UpdateWarningTimes(IEnumerable<int> account_ids)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(VpnAccountEntity.LastWarningTime)} = @now
                   where {nameof(VpnAccountEntity.Id)} in @account_ids
                   """;

        return ExecuteAsync(sql, new { account_ids, now = DateTime.Now });
    }

    public Task SetStatus(IEnumerable<int> account_ids, VpnAccountStatus status)
    {
        var sql = $"""
                   update {TableName}
                   set {nameof(VpnAccountEntity.Status)} = @status
                   where {nameof(VpnAccountEntity.Id)} in @account_ids
                   """;

        return ExecuteAsync(sql, new { account_ids, status = (byte)status });
    }
}
