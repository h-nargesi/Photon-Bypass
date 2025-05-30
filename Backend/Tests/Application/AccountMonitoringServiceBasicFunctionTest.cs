using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application;

public class AccountMonitoringServiceBasicFunctionTest : ServiceInitializer
{
    public new static HostApplicationBuilder Initialize()
    {
        var builder = ServiceInitializer.Initialize();

        var userRepo = new Mock<IPermanentUsersRepository>();
        userRepo.Setup(x => x.GetUser(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                _ = Users.TryGetValue(id, out var user);
                return Task.FromResult(user);
            });
        builder.Services.AddLazyScoped(s => userRepo.Object);

        var historyRepo = new Mock<IHistoryRepository>();
        historyRepo.Setup(x => x.Save(It.IsNotNull<HistoryEntity>()))
            .Returns(Task.CompletedTask);
        builder.Services.AddLazyScoped(s => historyRepo.Object);

        return builder;
    }

    [Fact]
    public async Task DeactiveAbandonedUsers_Check()
    {
        var builder = Initialize();

        var radiusSrv = new Mock<IRadiusService>();
        radiusSrv.Setup(x => x.ActivePermanentUser(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns<int, bool>((id, active) =>
            {
                Assert.True(Activate.TryGetValue(id, out var activate));
                Assert.Equal(active, activate);
                return Task.FromResult(true);
            });
        builder.Services.AddLazyScoped(s => radiusSrv.Object);

        Build(builder);

        var monitoring = CreateScope().GetRequiredService<IAccountMonitoringService>();

        await monitoring.DeactiveAbandonedUsers(PlanStates);
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
            Id = 4,
            PlanType = PlanType.Traffic,
            TotalData = 25,
            Username = "User4"
        },
    ];

    readonly static Dictionary<int, PermanentUserEntity?> Users = new()
    {
        { 1, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2) } },
        { 2, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2) } },
        { 3, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-79) } },
        { 4, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2) } },
        { 5, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2) } },
        { 6, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-79) } },
    };

    readonly static Dictionary<int, bool> Activate = new()
    {
        { 1, true },
        { 2, false },
        { 3, false },
        { 4, true },
        { 5, false },
        { 6, false },
    };
}
