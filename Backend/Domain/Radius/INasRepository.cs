namespace PhotonBypass.Domain.Radius;

public interface INasRepository
{
    Task<List<NasEntity>> GetAll();

    Task<NasEntity?> GetNasInfo(string ip);

    Task<Dictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips);
}
