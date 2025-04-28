namespace PhotonBypass.Domain.Management;

public interface IAccountMonitoringService
{
    Task NotifSendServices();

    Task DeactiveAbandonedUsers();
}
