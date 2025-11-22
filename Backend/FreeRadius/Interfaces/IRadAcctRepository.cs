using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.FreeRadius.Interfaces;

public interface IRadAcctRepository
{
    Task<IList<RadAcctEntity>> GetCurrentConnectionList(string username);
}
