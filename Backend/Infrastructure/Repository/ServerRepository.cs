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
            .Where($"{nameof(ServerEntity.Active)} == 1 and {nameof(ServerEntity.Features)} == @nas and {nameof(ServerEntity.IpAddress)} == @ip")
            .WithParameters(new { ip, nas = ServerFeature.Nas }));

        return result.FirstOrDefault();
    }

    public async Task<List<string>> GetAllActiveNasDomainInRealm(int? realm_id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(ServerEntity.Active)} == 1 and {nameof(ServerEntity.Features)} == @nas 
    and (@realm_id is null or {nameof(ServerEntity.RealmId)} == @realm_id)
""")
            .WithParameters(new { realm_id, nas = ServerFeature.Nas }));

        return result.Select(n => n.DomainName).ToList();
    }

    public async Task<Dictionary<int, List<ServerEntity>>> GetAllActiveRadiusInRealm(IEnumerable<int> realm_ids)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(ServerEntity.Active)} == 1 and {nameof(ServerEntity.Features)} == @radius and {nameof(ServerEntity.RealmId)} in (@realm_ids)")
            .WithParameters(new { realm_ids, radius = ServerFeature.Radius }));

        return result.GroupBy(k => k.RealmId)
            .ToDictionary(k => k.Key, v=> v.ToList());
    }
}