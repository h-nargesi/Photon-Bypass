namespace PhotonBypass.Domain.Radius;

public interface INasRepository
{
    Task<NasEntity?> GetNasInfo(string ip);

    Task<IDictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips);

    Task<bool> Exists(string ip);
}
