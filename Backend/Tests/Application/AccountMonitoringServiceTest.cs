using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using PhotonBypass.Test.MockOutSources;

namespace PhotonBypass.Test.Application;

public class AccountMonitoringServiceTest : ServiceInitializer
{
    [Fact]
    public async Task InactiveAbandonedUsers_Check()
    {
        using var scope = App.Services.CreateScope(); 

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var radius_srv_moq = scope.ServiceProvider.GetRequiredService<RadiusServiceMoq>();

        var inactive_users = new HashSet<int>() { 2, 3, 5 };
        radius_srv_moq.OnActivePermanentUser += (id, active, result) =>
        {
            Assert.True(result);
            Assert.False(active);
            Assert.Contains(id, inactive_users);
        };

        await monitoring.InactiveAbandonedUsers(PlanStates);
    }

    [Fact]
    public async Task NotifSendServices_Check()
    {
        using var scope = App.Services.CreateScope(); 

        scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        scope.ServiceProvider.GetRequiredService<ISocialMediaService>();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var email_service_moq = scope.ServiceProvider.GetRequiredService<EmailServiceMoq>();

        var emails = new HashSet<string>() { "User1", "User4" };
        email_service_moq.OnFinishServiceAlert += (fullname, username, email, type, left) =>
        {
            Assert.Contains(username, emails);
        };

        await monitoring.NotifSendServices(PlanStates);
    }

    private static readonly List<UserPlanStateEntity> PlanStates =
    [
        new()
        {
            Id = 1,
            PlanType = PlanType.Monthly,
            ExpirationDate = DateTime.Now.AddDays(-2),
            Username = "User1"
        },
        new()
        {
            Id = 2,
            PlanType = PlanType.Monthly,
            ExpirationDate = null,
            Username = "User2"
        },
        new()
        {
            Id = 3,
            PlanType = PlanType.Monthly,
            ExpirationDate = DateTime.Now.AddDays(-70),
            Username = "User3"
        },
        new()
        {
            Id = 4,
            PlanType = PlanType.Traffic,
            TotalData = 20,
            Username = "User4"
        },
        new()
        {
            Id = 5,
            PlanType = PlanType.Traffic,
            TotalData = null,
            Username = "User5"
        },
        new()
        {
            Id = 6,
            PlanType = PlanType.Traffic,
            TotalData = 25,
            Username = "User6"
        },
    ];
}
