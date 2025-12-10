using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Test.MockLocalRepository;
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
        var tik4_net = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();
        var account_repo = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        var other = false;
        var user_4_disabled_local = false;
        var user_4_deleted = false;
        var user_3_disabled = false;
        
        var plan_state_list = await plan_state_repo.GetAll();
        tik4_net.OnExecute += (command_text, parameters) =>
        {
            if (command_text.EndsWith("/remove") &&
                parameters.Any(parameter => parameter is { Name: "user", Value: "User4" }))
            {
                user_4_deleted = true;
            }            
            else if (command_text.EndsWith("/disable") && 
                parameters.Any(parameter => parameter is { Name: "user", Value: "User3" }))
            {
                user_3_disabled = true;
            }
            else
            {
                other = true;
            }
        };

        account_repo.OnSave += account =>
        {
            if (account is { Username: "User4", Active: false })
            {
                user_4_disabled_local = false;
            }
            else
            {
                other = true;
            }
        };

        await monitoring.InactiveAbandonedUsers(plan_state_list);
        
        Assert.True(user_4_disabled_local);
        Assert.True(user_4_deleted);
        Assert.True(user_3_disabled);
        Assert.False(other);
    }

    [Fact]
    public async Task NotifSendServices_Check()
    {
        using var scope = App.Services.CreateScope(); 

        scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        scope.ServiceProvider.GetRequiredService<ISocialMediaService>();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_service_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var plan_state_list = await plan_state_repo.GetAll();
        var finishing_list = plan_state_list.Where(plan => plan.IsFinishing()).ToList();
        
        var emails = new HashSet<string>() { "User1", "User4" };
        email_service_moq.OnSend += (mail) =>
        {
            // mail.To.Contains()
            // Assert.Contains(username, emails);
        };

        await monitoring.NotifSendServices(finishing_list);
    }
}
