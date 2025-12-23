using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Management;

public interface IAccountMonitoringService
{
    Task NotifSendServices(IEnumerable<PlanStateEntity> plan_states);

    Task InactiveAbandonedUsers(IEnumerable<PlanStateEntity> plan_state_list);
}
