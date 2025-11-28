using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class NasRepository(RadDbContext context) : DapperRepository<NasEntity>(context), INasRepository
{
    private static readonly string IpAddress = EntityExtensions.GetColumnName<NasEntity>(x => x.IpAddress);
    private static readonly string DomainName = EntityExtensions.GetColumnName<NasEntity>(x => x.DomainName);
    private static readonly string SshPassword = EntityExtensions.GetColumnName<NasEntity>(x => x.SshPassword);

    public async Task<List<NasEntity>> GetAll()
    {
        var result = await FindAsync(statement => statement
            .Where($"{SshPassword} is not null"));

        return [.. result];
    }

    public async Task<NasEntity?> GetNasInfo(string ip)
    {
        var result = await FindAsync(statement => statement
            .Where($"{SshPassword} is not null and {DomainName} is not null and {IpAddress} = @ip")
            .WithParameters(new { ip }));

        return result.FirstOrDefault();
    }

    public async Task<Dictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips)
    {
        var result = await FindAsync(statement => statement
            .Where($"{SshPassword} is not null and {IpAddress} in @ips")
            .WithParameters(new { ips }));

        return result.ToDictionary(x => x.IpAddress);
    }
}
