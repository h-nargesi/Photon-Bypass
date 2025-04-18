using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius.Repository;

class NasRepository(RadDbContext context) : DapperRepository<NasEntity>(context), INasRepository
{
    public async Task<IDictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.IpAddress)} in @ips")
            .WithParameters(new { ips }));

        return result.ToDictionary(x => x.IpAddress);
    }

    public async Task<bool> Exists(string ip)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(NasEntity.IpAddress)} = @ip")
            .WithParameters(new { ip }));

        return result.Any();
    }
}
