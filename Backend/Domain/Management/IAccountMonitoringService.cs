using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Management;

public interface IAccountMonitoringService
{
    static event Action<UserCheckingEvent>? OnUserChecking;

    Task NotifSendServices(IEnumerable<SessionStateEntity> plan_states);
    
    Task InactiveAbandonedUsers(IEnumerable<SessionStateEntity> plan_state_list);
}
