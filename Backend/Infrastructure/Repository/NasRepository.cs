using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class NasRepository(LocalDbContext context) : EditableRepository<NasEntity>(context), INasRepository
{
    public async Task<List<NasEntity>> GetAllActive()
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.Active)} == 1"));

        return [.. result];
    }

    public async Task<NasEntity?> GetActiveNasInfo(string ip)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.Active)} == 1 and {nameof(NasEntity.IpAddress)} == @ip")
            .WithParameters(new { ip }));

        return result.FirstOrDefault();
    }

    public async Task<Dictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.Active)} == 1 and {nameof(NasEntity.IpAddress)} in (@ips)")
            .WithParameters(new { ips }));

        return result.ToDictionary(k => k.IpAddress);
    }

    public async Task<List<NasEntity>> GetAllActiveInRealm(int realm_id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.Active)} == 1 and {nameof(NasEntity.RealmId)} == @realm_id")
            .WithParameters(new { realm_id }));

        return result.ToList();
    }

    public async Task<List<string>> GetAllActiveDomainInRealm(int? realm_id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.Active)} == 1 and {nameof(NasEntity.RealmId)} == @realm_id")
            .WithParameters(new { realm_id }));

        return result.Select(n => n.DomainName).ToList();
    }

    public async Task<Dictionary<int, List<NasEntity>>> GetAllActiveInRealm(IEnumerable<int> realm_ids)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.Active)} == 1 and {nameof(NasEntity.RealmId)} in (@realm_ids)")
            .WithParameters(new { realm_ids }));

        return result.GroupBy(k => k.RealmId)
            .ToDictionary(k => k.Key, v=> v.ToList());
    }
}