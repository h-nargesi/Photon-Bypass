using FluentAssertions;
using Moq;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

public class PlanApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GetPlanState_InvalidUsername()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanState("Invalid username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanState_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanState("InactiveUser7");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanState_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var plan_state = await plan_app.GetPlanState("User6");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Null(plan_state.Data.SimultaneousUserCount);
        Assert.Null(plan_state.Data.RemainsTitle);
        Assert.Null(plan_state.Data.RemainsTimePercent);
        Assert.Null(plan_state.Data.RemainsTrafficPercent);
    }

    [Fact]
    public async Task GetPlanState_User2()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var plan_state = await plan_app.GetPlanState("User2");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.SimultaneousUserCount);
        Assert.Null(plan_state.Data.RemainsTitle);
        Assert.Null(plan_state.Data.RemainsTrafficPercent);
        Assert.Null(plan_state.Data.RemainsTimePercent);
    }

    [Fact]
    public async Task GetPlanState_User1()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var plan_state = await plan_app.GetPlanState("User99");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.SimultaneousUserCount);
        Assert.Equal(99, plan_state.Data.RemainsTrafficPercent);
        Assert.Equal(92, plan_state.Data.RemainsTimePercent); // real value is 92.5%
    }

    [Fact]
    public async Task GetPlanInfo_InvalidUsername()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanInfo("Invalid username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanInfo_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanInfo("InactiveUser7");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanInfo_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var plan_state = await plan_app.GetPlanInfo("User6");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal("User6", plan_state.Data.Target);
        Assert.Null(plan_state.Data.SimultaneousUserCount);
        Assert.Null(plan_state.Data.Days);
        Assert.Null(plan_state.Data.Gigabytes);
        Assert.Null(plan_state.Data.Kind);
        Assert.Null(plan_state.Data.SupportsOvpnCert);
        Assert.Null(plan_state.Data.SupportsPasswordChange);
        Assert.Null(plan_state.Data.SupportsCredentialRotation);
    }

    [Fact]
    public async Task GetPlanInfo_User1()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var plan_state = await plan_app.GetPlanInfo("User99");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.SimultaneousUserCount);
        Assert.Equal(120, plan_state.Data.Days);
        Assert.Equal(1, plan_state.Data.Gigabytes);
        Assert.Equal("usermanager", plan_state.Data.Kind);
        Assert.True(plan_state.Data.SupportsOvpnCert);
        Assert.True(plan_state.Data.SupportsPasswordChange);
        Assert.False(plan_state.Data.SupportsCredentialRotation);
    }

    [Fact]
    public async Task GetPlanInfo_XuiRealm_ReportsCredentialRotationInsteadOfPasswordChange()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(2))
            .ReturnsAsync([new ServerEntity
            {
                Id = 21,
                IsActive = true,
                RealmId = 2,
                Name = "XuiServer",
                IpAddress = "10.0.1.21",
                DomainName = "srv21.photon-bypass.com",
                Features = ServerFeature.Xui | ServerFeature.V2ray,
            }]);

        // User2 is restricted to realm 2
        var plan_state = await plan_app.GetPlanInfo("User2");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal("xui", plan_state.Data.Kind);
        Assert.False(plan_state.Data.SupportsPasswordChange);
        Assert.True(plan_state.Data.SupportsCredentialRotation);
    }

    [Fact]
    public async Task GetPlanInfo_RealmWithoutRadius_ReportsNoKindAndNoPasswordChange()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        // User2 is restricted to realm 2 which has a NAS but no radius server
        var plan_state = await plan_app.GetPlanInfo("User2");

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Null(plan_state.Data.Kind);
        Assert.True(plan_state.Data.SupportsOvpnCert);
        Assert.False(plan_state.Data.SupportsPasswordChange);
    }

    [Fact]
    public async Task Renewal_InvalidUsername()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var func = () => plan_app.Renewal("InvalidUser", 2, 10, 20);
        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var func = () => plan_app.Renewal("InactiveUser7", 2, 10, 20);
        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(2, "User2", "User2");

        var estimate = await plan_app.Estimate("User2", 2, 120, 25);

        Assert.NotNull(estimate);

        var result = await plan_app.Renewal("User2", 2, 120, 25);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(1500, result.Data.CurrentPrice);
        Assert.NotNull(result.Data.InvoiceCode);
    }

    [Fact]
    public async Task Renewal_EmptyMoney()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(3, "User3", "User3");

        var estimate = await plan_app.Estimate("User3", 2, 120, 25);

        Assert.NotNull(estimate);

        var result = await plan_app.Renewal("User3", 2, 120, 25);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(-800, result.Data.CurrentPrice);
        Assert.NotNull(result.Data.InvoiceCode);
    }

    [Fact]
    public async Task Renewal_DependentSelfUser_IsForbidden()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(11, "DependentUser11", "DependentUser11");

        var func = () => plan_app.Renewal("DependentUser11", 2, 120, 25);

        (await func.Should().ThrowAsync<UserException>())
            .Which.HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task Estimate_DependentSelfUser_IsForbidden()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(11, "DependentUser11", "DependentUser11");

        var func = () => plan_app.Estimate("DependentUser11", 2, 120, 25);

        (await func.Should().ThrowAsync<UserException>())
            .Which.HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task Renewal_OnBehalf_IssuesInvoiceAndReadsBalanceOnPayer()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User2");

        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        wallet_moq.Data[99] =
        [
            new WalletEntity
            {
                Id = 901,
                AccountId = 99,
                Amount = 1500,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var estimate = (await plan_app.Estimate("User2", 2, 120, 25)).Data!;

        var result = await plan_app.Renewal("User2", 2, 120, 25);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(1500, result.Data.CurrentPrice);
        Assert.NotNull(result.Data.InvoiceCode);

        // D4: the invoice belongs to the paying manager, the action keeps the target
        var invoice = invoice_moq.Data.Single(i => i.Code == result.Data.InvoiceCode);
        Assert.Equal(99, invoice.AccountId);
        Assert.Equal("User2t|2u|120d|25g", invoice.Action);

        Assert.Equal(estimate.Price, invoice.TotalPrice);
    }

    [Fact]
    public async Task Renewal_Action_InvalidAction()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.Renewal(2, 1, "");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.Renewal(2, 1, "10u");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.Renewal(2, 1, "20d");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.Renewal(2, 1, "20g");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.Renewal(2, 1, "10u|20d");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.Renewal(2, 1, "10u|20g");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.Renewal(2, 1, "10d|20g");
        await func.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Renewal_Action()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User2");

        // replay path: account 99 pays for target User2
        wallet_moq.Data[99] =
        [
            new WalletEntity
            {
                Id = 902,
                AccountId = 99,
                Amount = 1500,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var estimate = await plan_app.Estimate("User2", 5, 120, 75);

        Assert.NotNull(estimate);

        var result = await plan_app.Renewal(99, 1, "User2t|5u|120d|75g");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(1500 - estimate.Data?.Price, result.Data.CurrentPrice);
        Assert.Null(result.Data.InvoiceCode);

        // D4: the debit leaves the payer wallet, the renewal lands on the target
        Assert.Contains(wallet_moq.Data[99], w =>
            w.Direction == BalanceDirection.Debit && w.Amount == estimate.Data!.Price);
        Assert.DoesNotContain(wallet_moq.Data[2], w => w.Direction == BalanceDirection.Debit);

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 2 && r.SimultaneousUser == 5);
        Assert.Equal(75, renewal.TrafficLimit / StaticValues.BytesInGigLong);
    }

    [Fact]
    public async Task Renewal_Action_InsufficientBalance()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        // payer (99) has no wallet at all: the money-need is judged on the payer
        var func = () => plan_app.Renewal(99, 1, "User3t|2u|120d|50g");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal_Action_PayerNotFound()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.Renewal(777, 1, "User2t|2u|120d|50g");

        await func.Should().ThrowAsync<UserException>();
    }
}
