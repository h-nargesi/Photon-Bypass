using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Management;

public interface IAccountMonitoringService
{
    static event Action<UserCheckingEvent>? OnUserChecking;

    Task NotifSendServices(IEnumerable<UserPlanStateEntity> planStateList);

    Task InactiveAbandonedUsers(IEnumerable<UserPlanStateEntity> planStateList);
}
