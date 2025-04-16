using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Database.Radius;

namespace PhotonBypass.Application.Database;

class NasRepository(IRadRepository<NasEntity> repository)
{
    public async Task<IDictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(NasEntity.IpAddress)} in @ips")
            .WithParameters(new { ips }));

        return result.ToDictionary(x => x.IpAddress);
    }

    public async Task<bool> Exists(string ip)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(NasEntity.IpAddress)} = @ip")
            .WithParameters(new { ip }));

        return result.Any();
    }
}
