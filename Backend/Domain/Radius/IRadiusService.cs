using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Vpn;

namespace PhotonBypass.Domain.Radius;

public interface IRadiusService : IDisposable
{
    Task ChangeOvpnPassword(string username, string password);

    Task SavePermenentUser(PermenantUserEntity user);

    Task<IList<TrafficDataRadius>> FetchTrafficData(DateTime index, TrafficDataRequestType type);
}
