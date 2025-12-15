using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class ServerRepository(LocalDbContext context) : EditableRepository<ServerEntity>(context), IServerRepository
{
    public async Task<ServerEntity?> GetActiveNasInfo(string ip)
    {
        await OpenAsync();
        
        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(ServerEntity.IsActive)} == 1
    and {nameof(ServerEntity.Features)} == ({nameof(ServerEntity.Features)} & @nas)
    and {nameof(ServerEntity.IpAddress)} == @ip")
""")
            .WithParameters(new { ip, nas = ServerFeature.Nas }));

        return result.FirstOrDefault();
    }

    public async Task<List<string>> GetAllActiveNasDomainInRealm(int? realm_id)
    {
        await OpenAsync();
        
        var sql = $"""
                  select {nameof(ServerEntity.DomainName)}
                  from {TableName}
                  where {nameof(ServerEntity.IsActive)} == 1 and {nameof(ServerEntity.Features)} = ({nameof(ServerEntity.Features)} & @nas)
                  """;
        
        if (realm_id.HasValue)
        {
            sql += $"""
                   {nameof(ServerEntity.RealmId)} == @realm_id
                   """;
        }

        var result = await QueryAsync(sql, new { nas = ServerFeature.Nas, realm_id });

        return result.Select(n => (string)n.DomainName).ToList();
    }

    public async Task<List<ServerEntity>> GetActiveNasInRealmOrAll(int? realm_id)
    {
        await OpenAsync();

        var result = await FindAsync(statement =>
        {
            statement
                .Where($"{nameof(ServerEntity.IsActive)} == 1 and {nameof(ServerEntity.Features)} = ({nameof(ServerEntity.Features)} & @nas)")
                .WithParameters(new { nas = ServerFeature.Nas });

            if (realm_id.HasValue)
            {
                statement
                    .Where($"{nameof(ServerEntity.RealmId)} == @realm_id")
                    .WithParameters(new { realm_id = realm_id.Value });

            }
        });

        return result.GroupBy(server => server.RealmId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.First())
            .Values
            .ToList();
    }

    public async Task<List<ServerEntity>> GetAllActiveRadius()
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(ServerEntity.IsActive)} == 1 and {nameof(ServerEntity.Features)} = ({nameof(ServerEntity.Features)} & @radius)")
            .WithParameters(new { radius = ServerFeature.Radius }));

        return [..result];
    }

    public async Task<List<ServerEntity>> GetActiveRadiusInRealmOrAll(int? realm_id)
    {
        await OpenAsync();

        var result = await FindAsync(statement =>
        {
            statement
                .Where($"{nameof(ServerEntity.IsActive)} == 1 and {nameof(ServerEntity.Features)} = ({nameof(ServerEntity.Features)} & @radius)")
                .WithParameters(new { radius = ServerFeature.Radius });

            if (realm_id.HasValue)
            {
                statement
                    .Where($"{nameof(ServerEntity.RealmId)} == @realm_id")
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
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(ServerEntity.IsActive)} == 1
    and {nameof(ServerEntity.Features)} == ({nameof(ServerEntity.Features)} & @nas)
    and {nameof(ServerEntity.RealmId)} in (@realm_ids)
""")
            .WithParameters(new { realm_ids, nas = ServerFeature.Nas }));

        return result.GroupBy(k => k.RealmId)
            .ToDictionary(k => k.Key, v=> v.ToList());
    }

    public async Task<Dictionary<string, int>> GetServerIdByIpAddress(IEnumerable<string> ips)
    {
        await OpenAsync();

        var sql = $"""
                   select {nameof(ServerEntity.Id)}, {nameof(ServerEntity.IpAddress)}
                   from {TableName}
                   where {nameof(ServerEntity.IpAddress)} in (@ips)
                   """;

        var list = await QueryAsync(sql, ips);

        return list.ToDictionary(pair => (string)pair.IpAddress, pair => (int)pair.Id);
    }

    public async Task<Dictionary<int, ServerEntity>> GetActiveRadiusInRealm(IEnumerable<int> realm_ids)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(ServerEntity.IsActive)} == 1
    and {nameof(ServerEntity.Features)} == ({nameof(ServerEntity.Features)} & @radius)
    and {nameof(ServerEntity.RealmId)} in (@realm_ids)
""")
            .WithParameters(new { realm_ids, radius = ServerFeature.Radius }));

        return result.GroupBy(k => k.RealmId)
            .ToDictionary(k => k.Key, v=> v.First());
    }
}