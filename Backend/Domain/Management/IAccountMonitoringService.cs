using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Management;

public interface IAccountMonitoringService
{
    Task NotifSendServices(IEnumerable<UserPlanStateEntity> planStateList);

    Task DeactiveAbandonedUsers(IEnumerable<UserPlanStateEntity> planStateList);
}
