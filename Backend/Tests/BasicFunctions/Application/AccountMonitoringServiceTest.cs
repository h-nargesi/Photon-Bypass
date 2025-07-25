﻿using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using PhotonBypass.Test.MockOutSources;

namespace PhotonBypass.Test.BasicFunctions.Application;

public class AccountMonitoringServiceTest : ServiceInitializer
{
    public AccountMonitoringServiceTest()
    {
        var builder = Initialize();
        AddDefaultServices(builder);
        Build(builder);
    }

    [Fact]
    public async Task DeactiveAbandonedUsers_Check()
    {
        var services = CreateScope();

        var monitoring = services.GetRequiredService<IAccountMonitoringService>();
        var radiusSrvMoq = services.GetRequiredService<RadiusServiceMoq>();

        var deactiveUsers = new HashSet<int>() { 2, 3, 5 };
        radiusSrvMoq.OnActivePermanentUser += (id, active, result) =>
        {
            Assert.True(result);
            Assert.False(active);
            Assert.Contains(id, deactiveUsers);
        };

        await monitoring.DeactiveAbandonedUsers(PlanStates);
    }

    [Fact]
    public async Task NotifSendServices_Check()
    {
        var services = CreateScope();

        services.GetRequiredService<IServerManagementService>();
        services.GetRequiredService<ISocialMediaService>();

        var monitoring = services.GetRequiredService<IAccountMonitoringService>();
        var emailServiceMoq = services.GetRequiredService<EmailServiceMoq>();

        var emails = new HashSet<string>() { "User1", "User4" };
        emailServiceMoq.OnFinishServiceAlert += (fullname, username, email, type, left) =>
        {
            Assert.Contains(username, emails);
        };

        await monitoring.NotifSendServices(PlanStates);
    }

    readonly static List<UserPlanStateEntity> PlanStates =
    [
        new UserPlanStateEntity {
            Id = 1,
            PlanType = PlanType.Monthly,
            ExpirationDate = DateTime.Now.AddDays(-2),
            Username = "User1"
        },
        new UserPlanStateEntity {
            Id = 2,
            PlanType = PlanType.Monthly,
            ExpirationDate = null,
            Username = "User2"
        },
        new UserPlanStateEntity {
            Id = 3,
            PlanType = PlanType.Monthly,
            ExpirationDate = DateTime.Now.AddDays(-70),
            Username = "User3"
        },
        new UserPlanStateEntity {
            Id = 4,
            PlanType = PlanType.Traffic,
            TotalData = 20,
            Username = "User4"
        },
        new UserPlanStateEntity {
            Id = 5,
            PlanType = PlanType.Traffic,
            TotalData = null,
            Username = "User5"
        },
        new UserPlanStateEntity {
            Id = 6,
            PlanType = PlanType.Traffic,
            TotalData = 25,
            Username = "User6"
        },
    ];
}
