using Dapper;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PlanStateRepository(LocalDbContext context) : DapperRepository<PlanStateEntity>(context), IPlanStateRepository
{
    private const string ReportView = "TotalPlanState";

    private const string MemberTable = "PanelUser";

    private const string GroupTable = "UserGroup";

    private const string RealmTable = "Realm";

    /// <summary>
    /// Docs/24 §5 sort whitelist — the caller's raw sort term never reaches
    /// the SQL. NULL (unlimited) sorts as the largest value on either
    /// direction, per the report requirement «نامحدود بزرگترین مقدار است».
    /// </summary>
    private static readonly Dictionary<string, string> ReportSortExpressions = new()
    {
        ["username"] = "tps.Username",
        ["owner"] = "pu.Username",
        ["realm"] = "coalesce(r.DisplayName, r.Name)",
        ["status"] = "tps.Status",
        ["users"] = "ISNULL(tps.SimultaneousUser, 255)",
        ["days"] = "ISNULL(tps.TimeLimitInDays, 32767)",
        ["traffic"] = "ISNULL(tps.TrafficLimit, 9223372036854775807)",
        ["rate"] = "ISNULL(tps.RateLimitInMeg, 255)",
        ["expiration"] = "ISNULL(tps.ExpirationDate, '9999-12-31T23:59:59')",
        ["trafficleft"] = "ISNULL(tps.TrafficLeft, 9223372036854775807)",
        ["lastconnect"] = "ISNULL(tps.LastConnectTime, '9999-12-31T23:59:59')",
        ["created"] = "tps.Created",
    };

    public async Task<List<PlanStateEntity>> GetAll()
    {
        return [.. await FindAsync()];
    }

    public async Task<PlanStateEntity?> GetPlanState(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanStateEntity.Id)} = @account_id")
            .WithParameters(new { account_id }));

        return result.FirstOrDefault();
    }

    public async Task<(List<VpnAccountReportRow> Rows, int TotalCount)> SearchAccountReport(
        int? owner_id, string? term, string? sort, bool descending, int page, int page_size)
    {
        var parameters = new DynamicParameters();
        var filters = new List<string>();

        if (owner_id.HasValue)
        {
            filters.Add("pu.Owner = @owner_id");
            parameters.Add("owner_id", owner_id.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            filters.Add("(tps.Username like @term or pu.Username like @term)");
            parameters.Add("term", $"%{term.Trim()}%");
        }

        var where = filters.Count > 0 ? "where " + string.Join(" and ", filters) : string.Empty;

        var sort_expression = ReportSortExpressions.GetValueOrDefault(
            (sort ?? "username").Trim().ToLowerInvariant(),
            ReportSortExpressions["username"]);

        var direction = descending ? "desc" : "asc";

        // latest band per account — same precedence as the user-list nesting
        // (top RowNumber desc); band rows without usage collapse into one
        var band_source = $"""
                           select tps.*
                                , ROW_NUMBER() over (partition by tps.Id order by tps.RowNumber desc) as BandRank
                           from {ReportView} tps
                           """;

        var total = await ExecuteScalarAsync<int>($"""
                                                   select count(*)
                                                   from ({band_source}) tps
                                                   join {MemberTable} pu on tps.UserId = pu.Id
                                                   where tps.BandRank = 1 {ConcatFilters(where)}
                                                   """, parameters);

        var sql = $"""
                   select tps.Id as {nameof(VpnAccountReportRow.AccountId)}
                          , tps.Username
                          , pu.Username as {nameof(VpnAccountReportRow.OwnerUsername)}
                          , pu.Name as {nameof(VpnAccountReportRow.OwnerName)}
                          , pu.Surname as {nameof(VpnAccountReportRow.OwnerSurname)}
                          , tps.RealmId
                          , r.Name as {nameof(VpnAccountReportRow.RealmName)}
                          , r.DisplayName as {nameof(VpnAccountReportRow.RealmDisplayName)}
                          , tps.Status
                          , tps.SimultaneousUser
                          , tps.TimeLimitInDays
                          , tps.TrafficLimit
                          , tps.RateLimitInMeg
                          , tps.ExpirationDate
                          , tps.TrafficLeft
                          , tps.LastConnectTime
                          , grp.Title as {nameof(VpnAccountReportRow.GroupTitle)}
                   from ({band_source}) tps
                   join {MemberTable} pu on tps.UserId = pu.Id
                   left join {GroupTable} grp on pu.UserGroupId = grp.Id
                   left join {RealmTable} r on tps.RealmId = r.Id
                   where tps.BandRank = 1 {ConcatFilters(where)}
                   order by {sort_expression} {direction}, tps.Id {direction}
                   offset @skip rows fetch next @take rows only
                   """;

        parameters.Add("skip", (page - 1) * page_size);
        parameters.Add("take", page_size);

        var rows = await QueryAsync<VpnAccountReportRow>(sql, parameters);

        return ([.. rows], total);
    }

    /// <summary>Turns the optional <c>where …</c> fragment into an <c>and …</c> continuation.</summary>
    private static string ConcatFilters(string where)
    {
        return string.IsNullOrEmpty(where) ? string.Empty : "and " + where["where ".Length..];
    }
}
