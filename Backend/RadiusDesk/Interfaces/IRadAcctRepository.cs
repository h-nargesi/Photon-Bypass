using PhotonBypass.RadiusDesk.Entity;

namespace PhotonBypass.RadiusDesk.Interfaces;

public interface IRadAcctRepository
{
    Task<IList<RadAcctEntity>> GetCurrentConnectionList(string username);
}
