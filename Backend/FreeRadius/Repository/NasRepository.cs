using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class NasRepository(RadDbContext context) : DapperRepository<NasEntity>(context), INasRepository
{
    readonly static string IpAddress = EntityExtensions.GetColumnName<NasEntity>(x => x.IpAddress);
    readonly static string DomainName = EntityExtensions.GetColumnName<NasEntity>(x => x.DomainName);
    readonly static string SshPassword = EntityExtensions.GetColumnName<NasEntity>(x => x.SshPassword);

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
