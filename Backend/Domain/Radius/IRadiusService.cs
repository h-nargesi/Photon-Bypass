using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Vpn;

namespace PhotonBypass.Domain.Radius;

public interface IRadiusService : IDisposable
{
    Task<string> GetOvpnPassword(string username);

    Task<bool> ChangeOvpnPassword(string username, string password);

    Task SavePermenentUser(PermanentUserEntity user);

    Task<IList<TrafficDataRadius>> FetchTrafficData(DateTime index, TrafficDataRequestType type);
}
