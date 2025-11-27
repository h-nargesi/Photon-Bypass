using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class ServerRepository(RadDbContext context) : DapperRepository<ServerEntity>(context), IServerRepository
{
    readonly static string IpAddress = EntityExtensions.GetColumnName<ServerEntity>(x => x.IpAddress);
    readonly static string DomainName = EntityExtensions.GetColumnName<ServerEntity>(x => x.DomainName);
    readonly static string SshPassword = EntityExtensions.GetColumnName<ServerEntity>(x => x.SshPassword);

    public async Task<List<ServerEntity>> GetAllActiveNas()
    {
        var result = await FindAsync(statement => statement
            .Where($"{SshPassword} is not null"));

        return [.. result];
    }

    public async Task<ServerEntity?> GetActiveNasInfo(string ip)
    {
        var result = await FindAsync(statement => statement
            .Where($"{SshPassword} is not null and {DomainName} is not null and {IpAddress} = @ip")
            .WithParameters(new { ip }));

        return result.FirstOrDefault();
    }

    public async Task<Dictionary<string, ServerEntity>> GetServerInfo(IEnumerable<string> ips)
    {
        var result = await FindAsync(statement => statement
            .Where($"{SshPassword} is not null and {IpAddress} in @ips")
            .WithParameters(new { ips }));

        return result.ToDictionary(x => x.IpAddress);
    }
}
