using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Vpn;

namespace PhotonBypass.Domain.Radius;

public interface IRadiusService : IDisposable
{
    Task<string> GetOvpnPassword(int user_id);

    Task<bool> ChangeOvpnPassword(string username, string password);

    Task SavePermenentUser(PermanentUserEntity user);

    Task<IList<TrafficDataRadius>> FetchTrafficData(DateTime index, TrafficDataRequestType type);

    Task SetRestrictedServer(int user_id, string? server_ip);

    Task UpdateUserDataUsege(int user_id);

    Task SetUserDate(int user_id, DateTime from, DateTime to);

    Task InsertTopUp(string target, PlanType type, int value);
}
