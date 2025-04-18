namespace PhotonBypass.Domain.Profile;

public interface IRadAcctRepository
{
    Task<IList<RadAcctEntity>> GetCurrentConnectionList(string username);
}
