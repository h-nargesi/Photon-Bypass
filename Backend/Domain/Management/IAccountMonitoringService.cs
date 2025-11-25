using PhotonBypass.Domain.Session.Entity;

namespace PhotonBypass.Domain.Management;

public interface ISessionMonitoringService
{
    static event Action<UserCheckingEvent>? OnUserChecking;

    Task NotifSendServices(IEnumerable<SessionStateEntity> plan_states);
    
    Task InactiveAbandonedUsers(IEnumerable<SessionStateEntity> plan_state_list);
}
