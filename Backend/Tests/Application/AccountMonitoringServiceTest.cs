using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Test.MockServerBridge;

namespace PhotonBypass.Test.Application;

public class AccountMonitoringServiceTest : ServiceInitializer
{
    [Fact]
    public async Task InactiveAbandonedUsers_Check()
    {
        using var scope = App.Services.CreateScope(); 

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var tik4net = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();

        var plan_state_list = await plan_state_repo.GetAll();
        tik4net.OnDelete += (command_text, parameters) =>
        {

        };

        await monitoring.InactiveAbandonedUsers(plan_state_list);
    }

    [Fact]
    public async Task NotifSendServices_Check()
    {
        using var scope = App.Services.CreateScope(); 

        scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        scope.ServiceProvider.GetRequiredService<ISocialMediaService>();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var email_service_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var emails = new HashSet<string>() { "User1", "User4" };
        email_service_moq.OnSend += (mail) =>
        {
            // mail.To.Contains()
            // Assert.Contains(username, emails);
        };

        await monitoring.NotifSendServices(PlanStates);
    }
}
