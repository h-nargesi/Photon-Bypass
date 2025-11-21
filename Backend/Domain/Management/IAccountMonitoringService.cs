using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Management;

public interface IAccountMonitoringService
{
    static event Action<UserCheckingEvent>? OnUserChecking;

    Task NotifSendServices(IEnumerable<UserPlanStateEntity> plan_state_list);

    Task InactiveAbandonedUsers(IEnumerable<UserPlanStateEntity> plan_state_list);
}
