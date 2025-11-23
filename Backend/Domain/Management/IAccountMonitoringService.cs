using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Profile.Model;

namespace PhotonBypass.Domain.Management;

public interface IAccountMonitoringService
{
    static event Action<UserCheckingEvent>? OnUserChecking;

    Task NotifSendServices(IEnumerable<AccountStateEntity> plan_states);
    
    Task InactiveAbandonedUsers(IEnumerable<AccountStateEntity> plan_state_list);
}
