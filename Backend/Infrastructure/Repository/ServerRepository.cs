using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class ServerRepository(LocalDbContext context, IMemoryCache cache) : EditableRepository<ServerEntity>(context), IServerRepository
{
    private static readonly TimeSpan RadiusCacheDuration = TimeSpan.FromSeconds(30);
    public async Task<ServerEntity?> GetActiveNasInfo(string ip)
    {
        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(ServerEntity.IsActive)} = 1
    and ({nameof(ServerEntity.Features)} & @nas) > 0
    and {nameof(ServerEntity.IpAddress)} = @ip")
""")
            .WithParameters(new { ip, nas = ServerFeature.Nas }));

        return result.FirstOrDefault();
    }

    public async Task<List<string>> GetAllActiveNasDomainInRealm(int? realm_id)
    {
        var sql = $"""
                  select {nameof(ServerEntity.DomainName)}
                  from {TableName}
                  where {nameof(ServerEntity.IsActive)} = 1 
                    and ({nameof(ServerEntity.Features)} & @nas) > 0
                  """;

        if (realm_id.HasValue)
        {
            sql += $"and {nameof(ServerEntity.RealmId)} = @realm_id";
        }

        var result = await QueryAsync(sql, new { nas = ServerFeature.Nas, realm_id });

        return result.Select(n => (string)n.DomainName).ToList();
    }

    public async Task<List<ServerEntity>> GetActiveNasInRealmOrAll(int? realm_id)
    {
        var result = await FindAsync(statement =>
        {
            statement
                .Where($"{nameof(ServerEntity.IsActive)} = 1 and ({nameof(ServerEntity.Features)} & @nas) > 0")
                .WithParameters(new { nas = ServerFeature.Nas });

            if (realm_id.HasValue)
            {
                statement
                    .Where($"{nameof(ServerEntity.RealmId)} = @realm_id")
                    .WithParameters(new { realm_id = realm_id.Value });

            }
        });

        return result.GroupBy(server => server.RealmId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.First())
            .Values
            .ToList();
    }

    public Task<List<ServerEntity>> GetAllActiveRadius()
    {
        return cache.GetOrCreateAsync(nameof(GetAllActiveRadius), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RadiusCacheDuration;

            return await LoadAllActiveRadius();
        });
    }

    public Task<List<ServerEntity>> GetActiveRadiusInRealmOrAll(int? realm_id)
    {
        return cache.GetOrCreateAsync($"{nameof(GetActiveRadiusInRealmOrAll)}:{realm_id}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RadiusCacheDuration;

            return await LoadActiveRadiusInRealmOrAll(realm_id);
        });
    }

    private async Task<List<ServerEntity>> LoadAllActiveRadius()
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(ServerEntity.IsActive)} = 1 and ({nameof(ServerEntity.Features)} & @radius) > 0")
            .WithParameters(new { radius = ServerFeature.Radius }));

        return [..result];
    }

    private async Task<List<ServerEntity>> LoadActiveRadiusInRealmOrAll(int? realm_id)
    {
        var result = await FindAsync(statement =>
        {
            statement
                .Where($"{nameof(ServerEntity.IsActive)} = 1 and ({nameof(ServerEntity.Features)} & @radius) > 0")
                .WithParameters(new { radius = ServerFeature.Radius });

            if (realm_id.HasValue)
            {
                statement
                    .Where($"{nameof(ServerEntity.RealmId)} = @realm_id")
                    .WithParameters(new { realm_id = realm_id.Value });

            }
        });

        return result.GroupBy(server => server.RealmId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.First())
            .Values
            .ToList();
    }

    public async Task<Dictionary<int, List<ServerEntity>>> GetAllActiveNasInRealm(IEnumerable<int> realm_ids)
    {
        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(ServerEntity.IsActive)} = 1
    and ({nameof(ServerEntity.Features)} & @nas) > 0
    and {nameof(ServerEntity.RealmId)} in (@realm_ids)
""")
            .WithParameters(new { realm_ids, nas = ServerFeature.Nas }));

        return result.GroupBy(k => k.RealmId)
            .ToDictionary(k => k.Key, v=> v.ToList());
    }

    public async Task<Dictionary<string, (int, int)>> GetServerIdByIpAddress(IEnumerable<string> ips)
    {
        var sql = $"""
                   select {nameof(ServerEntity.Id)}, {nameof(ServerEntity.RealmId)}, {nameof(ServerEntity.IpAddress)}
                   from {TableName}
                   where {nameof(ServerEntity.IpAddress)} in (@ips)
                   """;

        var list = await QueryAsync(sql, ips);

        return list.ToDictionary(pair => (string)pair.IpAddress, pair => ((int)pair.Id, (int)pair.RealmId));
    }

    public async Task<Dictionary<int, ServerEntity>> GetActiveRadiusInRealm(IEnumerable<int> realm_ids)
    {
        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(ServerEntity.IsActive)} = 1
    and ({nameof(ServerEntity.Features)} & @radius) > 0
    and {nameof(ServerEntity.RealmId)} in (@realm_ids)
""")
            .WithParameters(new { realm_ids, radius = ServerFeature.Radius }));

        return result.GroupBy(k => k.RealmId)
            .ToDictionary(k => k.Key, v=> v.First());
    }
}