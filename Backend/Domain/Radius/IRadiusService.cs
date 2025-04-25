using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Vpn;

namespace PhotonBypass.Domain.Radius;

public interface IRadiusService : IDisposable
{
    Task<bool> ActivePermanentUser(int user_id, int cloud_id, bool active);

    Task<string?> GetOvpnPassword(int user_id, int cloud_id);

    Task<bool> ChangeOvpnPassword(int user_id, string password);

    Task<bool> SavePermenentUser(PermanentUserEntity user);

    Task<IList<TrafficDataRadius>> FetchTrafficData(DateTime index, TrafficDataRequestType type);

    Task<bool> SetRestrictedServer(int user_id, string? server_ip);

    Task<bool> UpdateUserDataUsege(int user_id);

    Task<bool> SetUserDate(int user_id, DateTime from, DateTime to);

    Task<bool> InsertTopUpAndMakeActive(string target, PlanType type, int value);
}
