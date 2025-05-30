using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
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
        userRepo.Setup(x => x.GetUsersContactInfo(It.IsAny<IEnumerable<int>>()))
            .Returns(Task.FromResult(
                (IDictionary<int, (string?, string?)>)Users.ToDictionary(k => k.Key, v => (v.Value?.Phone, v.Value?.Email))));
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

    [Fact]
    public async Task NotifSendServices_Check()
    {
        var builder = Initialize();

        var radiusSrv = new Mock<IRadiusService>();
        builder.Services.AddLazyScoped(s => radiusSrv.Object);

        var accountRepo = new Mock<IAccountRepository>();
        accountRepo.Setup(x => x.GetAccounts(It.IsAny<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(s => Task.FromResult(
                (IDictionary<int, AccountEntity>)
                Users.ToDictionary(k => k.Key, v => new AccountEntity { WarningTimes = DateTime.Now.AddHours(-5 * v.Key) })));
        builder.Services.AddLazyScoped(x => accountRepo.Object);

        var emailSrv = new Mock<IEmailService>();
        emailSrv.Setup(x => x.FinishServiceAlert(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PlanType>(), It.IsAny<string>()))
            .Returns<string, string, string, PlanType, string>((fullname, username, email, type, remains) =>
            {
                var plan = PlanStates.FirstOrDefault(x => x.Username == username);
                Assert.NotNull(plan);
                Assert.True(plan.Id > 4);
                Assert.NotNull(email);

                return Task.CompletedTask;
            });
        builder.Services.AddLazyScoped(x => emailSrv.Object);

        Build(builder);

        var monitoring = CreateScope().GetRequiredService<IAccountMonitoringService>();

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

    readonly static Dictionary<int, PermanentUserEntity?> Users = new()
    {
        { 1, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2), Phone = "11111", Email = "user1@mail.com" } },
        { 2, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2), Phone = "22222", Email = "user2@mail.com" } },
        { 3, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-79), Phone = "33333", Email = "user3@mail.com" } },
        { 4, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2), Phone = "44444", Email = "user4@mail.com" } },
        { 5, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-2), Phone = "55555", Email = null } },
        { 6, new PermanentUserEntity { LastAcceptTime = DateTime.Now.AddDays(-79), Phone = null, Email = "user6@mail.com" } },
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
