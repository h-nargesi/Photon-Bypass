using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

class NasRepository(RadDbContext context) : DapperRepository<NasEntity>(context), INasRepository
{
    readonly static string IpAddress = EntityExtensions.GetColumnName<NasEntity>(x => x.IpAddress);

    public async Task<NasEntity?> GetNasInfo(string ip)
    {
        var result = await FindAsync(statement => statement
            .Where($"{IpAddress} = @ip")
            .WithParameters(new { ip }));

        return result.FirstOrDefault();
    }

    public async Task<IDictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips)
    {
        var result = await FindAsync(statement => statement
            .Where($"{IpAddress} in @ips")
            .WithParameters(new { ips }));

        return result.ToDictionary(x => x.IpAddress);
    }

    public async Task<bool> Exists(string ip)
    {
        var result = await FindAsync(statement => statement
            .Where($"{IpAddress} = @ip")
            .WithParameters(new { ip }));

        return result.Any();
    }
}
