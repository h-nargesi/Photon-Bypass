using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Vpn;

namespace PhotonBypass.Domain.Radius;

public interface IRadiusService : IDisposable
{
    Task<bool> ActivePermanentUser(int user_id, bool active);

    Task<string?> GetOvpnPassword(int user_id);

    Task<bool> ChangeOvpnPassword(int user_id, string password);

    Task<bool> SaveUserBaiscInfo(PermanentUserEntity user);

    Task<bool> SaveUserPersonalInfo(PermanentUserEntity user);

    Task<bool> RegisterPermenentUser(PermanentUserEntity user, string password);

    Task<TrafficDataRadius[]> FetchTrafficData(string username, DateTime index, TrafficDataRequestType type);

    Task<bool> SetRestrictedServer(string username, string? server_ip);

    Task<bool> UpdateUserDataUsege(string username, long total_data);

    Task<bool> InsertTopUpAndMakeActive(int user_id, PlanType type, int value, string? comment = null);
}
