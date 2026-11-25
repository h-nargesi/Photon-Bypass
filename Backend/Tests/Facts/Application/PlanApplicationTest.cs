using FluentAssertions;
using Moq;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Static;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.Application;

public class PlanApplicationTest : UnitLevelServiceInitializer
{
    // fixtures keep the 1:1:1 mapping panel-user id == vpn-account id == plan id

    [Fact]
    public async Task GetPlanState_UnknownAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanState(12345);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal_SuspendedVpnAccount_IsRefusedWithFriendlyMessage()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var vpn_account_moq = scope.ServiceProvider.GetRequiredService<VpnAccountRepositoryMoq>();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();

        // D-A: a VPN-suspended account keeps portal access but renewal is
        // blocked until a manager reactivates it (Docs/22 D12: per account)
        vpn_account_moq.Data.First(account => account.Username == "User2").Status = VpnAccountStatus.Suspended;

        job_context.InjectJobContext(99, "User99", "User2");

        (await Assert.ThrowsAsync<UserException>(() => plan_app.SettlePurchase(99, null, "User2t|1u|30d|25g")))
            .UserMessage.Should().Contain("سرویس VPN شما توسط مدیریت غیرفعال است");

        Assert.Equal(VpnAccountStatus.Suspended, vpn_account_moq.Data.First(account => account.Username == "User2").Status);
    }

    [Fact]
    public async Task GetPlanState_DeactivatedAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanState(7);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanState_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var plan_state = await plan_app.GetPlanState(6);

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
        var plan_state = await plan_app.GetPlanState(2);

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
        var plan_state = await plan_app.GetPlanState(99);

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.SimultaneousUserCount);
        Assert.Equal(99, plan_state.Data.RemainsTrafficPercent);
        Assert.Equal(92, plan_state.Data.RemainsTimePercent); // real value is 92.5%
    }

    [Fact]
    public async Task GetPlanInfo_UnknownAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanInfo(12345);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanInfo_DeactivatedAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.GetPlanInfo(7);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetPlanInfo_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var plan_state = await plan_app.GetPlanInfo(6);

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal("User6", plan_state.Data.Target);
        Assert.Equal(6, plan_state.Data.AccountId);
        Assert.Equal(1, plan_state.Data.RealmId);
        Assert.False(plan_state.Data.RequiresRealmChoice);
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
        var plan_state = await plan_app.GetPlanInfo(99);

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
    public async Task GetPlanInfo_StaleAccount_RequiresRealmChoice()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        // User3 last connected exactly one day beyond the 7-day window
        var plan_state = await plan_app.GetPlanInfo(3);

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.True(plan_state.Data.RequiresRealmChoice);
    }

    [Fact]
    public async Task GetPlanInfo_XuiRealm_ReportsCredentialRotationInsteadOfPasswordChange()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(1))
            .ReturnsAsync([new ServerEntity
            {
                Id = 21,
                IsActive = true,
                RealmId = 1,
                Name = "XuiServer",
                IpAddress = "10.0.1.21",
                DomainName = "srv21.photon-bypass.com",
                Features = ServerFeature.Xui | ServerFeature.V2ray,
            }]);

        // User2's VPN account lives on realm 1
        var plan_state = await plan_app.GetPlanInfo(2);

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Equal(1, plan_state.Data.RealmId);
        Assert.Equal("xui", plan_state.Data.Kind);
        Assert.False(plan_state.Data.SupportsPasswordChange);
        Assert.True(plan_state.Data.SupportsCredentialRotation);
    }

    [Fact]
    public async Task GetPlanInfo_RealmWithoutRadius_ReportsNoKindAndNoPasswordChange()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        // mock the account realm with a NAS but no radius server
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();
        server_moq
            .Setup(x => x.GetActiveRadiusInRealmOrAll(1))
            .ReturnsAsync([]);

        var plan_state = await plan_app.GetPlanInfo(2);

        Assert.NotNull(plan_state);
        Assert.Equal(2, plan_state.Code / 100);
        Assert.NotNull(plan_state.Data);
        Assert.Null(plan_state.Data.Kind);
        Assert.True(plan_state.Data.SupportsOvpnCert);
        Assert.False(plan_state.Data.SupportsPasswordChange);
    }

    [Fact]
    public async Task Renewal_UnknownAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();

        job_context.InjectJobContext(12345, "UnknownUser", "UnknownUser");

        var func = () => plan_app.Renewal(12345, 2, 10, 20, null);
        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal_DeactivatedAccount()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();

        job_context.InjectJobContext(7, "InactiveUser7", "InactiveUser7");

        var func = () => plan_app.Renewal(7, 2, 10, 20, null);
        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal_StaleAccountWithoutRealmChoice_IsRefused()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();

        // User3's last activity is a day beyond the 7-day window (D6)
        job_context.InjectJobContext(3, "User3", "User3");

        var func = () => plan_app.Renewal(3, 2, 120, 25, null);

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("سرور");
    }

    [Fact]
    public async Task Renewal()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(2, "User2", "User2");

        var estimate = await plan_app.Estimate(2, 2, 120, 25);

        Assert.NotNull(estimate);

        var result = await plan_app.Renewal(2, 2, 120, 25, null);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(1500, result.Data.CurrentPrice);
        Assert.NotNull(result.Data.InvoiceCode);
    }

    [Fact]
    public async Task Renewal_SendsConfirmationEmailToThePlanOwner()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();

        // replay path: manager 99 pays, the confirmation goes to plan owner User2
        job_context.InjectJobContext(99, "User99", "User2");

        wallet_moq.Data[99] =
        [
            new WalletEntity
            {
                Id = 902,
                UserId = 99,
                Amount = 1500,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        EmailMessage? sent = null;
        scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>().OnSend += mail => sent = mail;

        var result = await plan_app.SettlePurchase(99, 1, "User2t|2u|120d|25g");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);

        // the confirmation goes to the plan owner's validated address,
        // not to the payer
        sent.Should().NotBeNull();
        sent!.ToAddress.Should().Be("user2@gmail.com");
        sent.Subject.Should().Contain("تمدید");
        sent.HtmlBody.Should().Contain("25 گیگی");
        sent.PlainTextBody.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Renewal_EmailFailure_DoesNotFailTheCommittedRenewal()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User2");

        wallet_moq.Data[99] =
        [
            new WalletEntity
            {
                Id = 902,
                UserId = 99,
                Amount = 1500,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>().OnSend +=
            _ => throw new InvalidOperationException("smtp unavailable");

        var result = await plan_app.SettlePurchase(99, 1, "User2t|2u|120d|25g");

        // the plan is committed and the money is gone: a failed email must
        // not turn the settled renewal into an error
        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Renewal_EmptyMoney()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        // User3 is stale (D6): the realm re-pick travels with the invoice
        job_context.InjectJobContext(3, "User3", "User3");

        var estimate = await plan_app.Estimate(3, 2, 120, 25);

        Assert.NotNull(estimate);

        var result = await plan_app.Renewal(3, 2, 120, 25, 1);

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

        var func = () => plan_app.Renewal(11, 2, 120, 25, null);

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

        var func = () => plan_app.Estimate(11, 2, 120, 25);

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
                UserId = 99,
                Amount = 1500,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var estimate = (await plan_app.Estimate(2, 2, 120, 25)).Data!;

        var result = await plan_app.Renewal(2, 2, 120, 25, null);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(1500, result.Data.CurrentPrice);
        Assert.NotNull(result.Data.InvoiceCode);

        // D4: the invoice belongs to the paying manager, the action keeps the target
        var invoice = invoice_moq.Data.Single(i => i.Code == result.Data.InvoiceCode);
        Assert.Equal(99, invoice.UserId);
        Assert.Equal(2, invoice.VpnAccountId);
        Assert.Equal("User2t|2u|120d|25g", invoice.Action);

        Assert.Equal(estimate.Price, invoice.TotalPrice);
    }

    [Fact]
    public async Task SettlePurchase_InvalidAction()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.SettlePurchase(2, 1, "");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.SettlePurchase(2, 1, "10u");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.SettlePurchase(2, 1, "20d");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.SettlePurchase(2, 1, "20g");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.SettlePurchase(2, 1, "10u|20d");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.SettlePurchase(2, 1, "10u|20g");
        await func.Should().ThrowAsync<Exception>();

        func = () => plan_app.SettlePurchase(2, 1, "10d|20g");
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
                UserId = 99,
                Amount = 1500,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var estimate = await plan_app.Estimate(2, 5, 120, 75);

        Assert.NotNull(estimate);

        var result = await plan_app.SettlePurchase(99, 1, "User2t|5u|120d|75g");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal(1500 - estimate.Data?.Price, result.Data.CurrentPrice);
        Assert.Null(result.Data.InvoiceCode);

        // D4: the debit leaves the payer wallet, the renewal lands on the target
        Assert.Contains(wallet_moq.Data[99], w =>
            w.Direction == BalanceDirection.Debit && w.Amount == estimate.Data!.Price);
        Assert.DoesNotContain(wallet_moq.Data[2], w => w.Direction == BalanceDirection.Debit);

        var renewal = renewal_moq.Data.Single(r => r.VpnAccountId == 2 && r.SimultaneousUser == 5);
        Assert.Equal(75, renewal.TrafficLimit / StaticValues.BytesInGigLong);
    }

    [Fact]
    public async Task Renewal_Action_InsufficientBalance()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        // payer (99) has no wallet at all: the money-need is judged on the payer
        var func = () => plan_app.SettlePurchase(99, 1, "User3t|2u|120d|50g");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Renewal_Action_PayerNotFound()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        var func = () => plan_app.SettlePurchase(777, 1, "User2t|2u|120d|50g");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task NewAccount_IssuesInvoiceWithRealmAndSuggestedUsername()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(6, "User6", "User6");

        scope.ServiceProvider.GetRequiredService<VpnAccountRepositoryMoq>()
            .Data.RemoveAll(account => account.UserId == 6);

        var result = await plan_app.NewAccount(username: null, realm_id: 1, users: 1, days: 30, gigabytes: 25);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.InvoiceCode);

        // D9: the new-account invoice carries the D3 suggestion (bare panel
        // username — User6 owns no VPN account yet) and the chosen realm
        var invoice = invoice_moq.Data.Single(i => i.Code == result.Data.InvoiceCode);
        Assert.Equal(6, invoice.UserId);
        Assert.Null(invoice.VpnAccountId);
        Assert.Equal("n|User6t|User6o|1u|30d|25g|1r", invoice.Action);
    }

    [Fact]
    public async Task NewAccount_RejectsForeignUsername()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(6, "User6", "User6");

        // D3: the VPN username must start with the owner's panel username
        var func = () => plan_app.NewAccount(username: " somebody-else", realm_id: 1, users: 1, days: 30, gigabytes: 25);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task NewAccount_RejectsTakenUsername()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(6, "User6", "User6");

        // User6 already owns the "User6" VPN username
        var func = () => plan_app.NewAccount(username: "User6", realm_id: 1, users: 1, days: 30, gigabytes: 25);

        (await func.Should().ThrowAsync<UserException>())
            .Which.HttpCode.Should().Be(409);
    }

    [Fact]
    public async Task NewAccount_DependentSelfUser_IsForbidden()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(11, "DependentUser11", "DependentUser11");

        var func = () => plan_app.NewAccount(username: "DependentUser11", realm_id: 1, users: 1, days: 30, gigabytes: 25);

        (await func.Should().ThrowAsync<UserException>())
            .Which.HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task SettleNewAccount_CreatesVpnAccountAndFirstRenewalAtomically()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var vpn_account_moq = scope.ServiceProvider.GetRequiredService<VpnAccountRepositoryMoq>();
        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        job_context.InjectJobContext(6, "User6", "User6");

        scope.ServiceProvider.GetRequiredService<VpnAccountRepositoryMoq>()
            .Data.RemoveAll(account => account.UserId == 6);

        wallet_moq.Data[6] =
        [
            new WalletEntity
            {
                Id = 903,
                UserId = 6,
                Amount = 2000,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var before = vpn_account_moq.Data.Count;

        var result = await plan_app.SettlePurchase(6, null, "n|User6t|User6o|1u|30d|25g|1r", 555);

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);

        // D9: the VPN account and its first renewal exist together, pinned to
        // the chosen realm with the D3 username
        var account = vpn_account_moq.Data.Single(a => a.Username == "User6");
        Assert.Equal(6, account.UserId);
        Assert.Equal(1, account.RealmId);
        Assert.Equal(VpnAccountStatus.Active, account.Status);
        Assert.False(string.IsNullOrEmpty(account.VpnPassword));

        var renewal = renewal_moq.Data.Single(r => r.VpnAccountId == account.Id);
        Assert.Equal(25, renewal.TrafficLimit / StaticValues.BytesInGigLong);
        // monthly pricing derives the expiry from the traffic share (Docs/17):
        // 25GB/1u → 60 + 30 - 0 = 90 days
        Assert.Equal((short)90, renewal.TimeLimitInDays);
        Assert.NotNull(renewal.ExpirationDate);
        Assert.Equal(25, renewal.CumulativeTrafficLeft / StaticValues.BytesInGigLong);

        // the wallet debit happened on the payer (the radius push itself is
        // covered by RenewalStackingTest/SettleRenewal below)
        Assert.Contains(wallet_moq.Data[6], w => w.Direction == BalanceDirection.Debit && w.InvoiceCode == 555);
    }

    [Fact]
    public async Task SettleRenewal_StaleAccount_MigratesRealm()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var vpn_account_moq = scope.ServiceProvider.GetRequiredService<VpnAccountRepositoryMoq>();

        // User3 is stale (last activity one day beyond the 7-day window)
        job_context.InjectJobContext(99, "User99", "User3");

        wallet_moq.Data[99] =
        [
            new WalletEntity
            {
                Id = 904,
                UserId = 99,
                Amount = 3000,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var result = await plan_app.SettlePurchase(99, null, "User3t|2u|30d|25g|1r");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);

        // D6: username and stacked state travel to the user-chosen realm —
        // the real dispatcher pins the new realm before the repo save
        var account = vpn_account_moq.Data.Single(a => a.Username == "User3");
        Assert.Equal(1, account.RealmId);
    }

    [Fact]
    public async Task SuggestUsername_Scenarios()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        // user with no VPN account yet: the bare panel username
        job_context.InjectJobContext(6, "User6", "User6");

        scope.ServiceProvider.GetRequiredService<VpnAccountRepositoryMoq>()
            .Data.RemoveAll(account => account.UserId == 6);

        (await plan_app.SuggestUsername()).Data.Should().Be("User6");

        // user whose bare username is taken by their own VPN account: first
        // free incremental suffix
        job_context.InjectJobContext(2, "User2", "User2");
        (await plan_app.SuggestUsername()).Data.Should().Be("User22");
    }
}
