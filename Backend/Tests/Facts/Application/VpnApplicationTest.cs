using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Test.Mock.MockXuiPanel;
using PhotonBypass.Tools;
using System.IO.Compression;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.Application;

public class VpnApplicationTest : UnitLevelServiceInitializer
{
    private static readonly DateTime Today = DateTime.Now.Date;

    [Fact]
    public async Task ChangeVpnPassword_InvlaidAccount()
    {
        using var scope = App.Services.CreateScope();
        var vpn_app = scope.ServiceProvider.GetRequiredService<IVpnApplication>();
        var func = () => vpn_app.ChangeVpnPassword("Invalid Username", "new_password");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangeVpnPassword_DisabledAccount()
    {
        using var scope = App.Services.CreateScope();
        var vpn_app = scope.ServiceProvider.GetRequiredService<IVpnApplication>();
        var func = () => vpn_app.ChangeVpnPassword("InactiveUser7", "new_password");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangeVpnPassword_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var vpn_app = scope.ServiceProvider.GetRequiredService<IVpnApplication>();
        var func = () => vpn_app.ChangeVpnPassword("User6", "new_password");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangeVpnPassword()
    {
        const string new_password = "new-password";

        using var scope = App.Services.CreateScope();

        var is_saved = false;
        var tik4_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();
        tik4_moq.OnExecute += (command_text, parameters) =>
        {
            is_saved = true;
            Assert.Equal("/user-manager/user/set", command_text);
            Assert.Equal(2, parameters.Count);
            Assert.Equal("1", parameters.Where(p => p.Name == ".id").Select(p => p.Value).FirstOrDefault());
            Assert.Equal(new_password, parameters.Where(p => p.Name == "password").Select(p => p.Value).FirstOrDefault());
        };

        AccountEntity? saved_account = null;
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        account_moq.OnSave += account => saved_account = account;

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User99", new_password);

        Assert.True(is_saved);
        Assert.Equal(2, data.Code / 100);
        Assert.NotNull(saved_account);
        Assert.Equal("User99", saved_account.Username);
        Assert.Equal(new_password, saved_account.VpnPassword);
    }

    [Fact]
    public async Task ChangeVpnPassword_RadiusWithoutPassword_BlockedInApplication()
    {
        using var scope = App.Services.CreateScope();

        var is_saved = false;
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().OnSave += _ => is_saved = true;

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User99", "");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("کلمه عبور خالی است");
        Assert.False(is_saved);
    }

    [Fact]
    public async Task ChangeVpnPassword_RealmWithoutRadiusServer_BlockedByGate()
    {
        using var scope = App.Services.CreateScope();

        var is_saved = false;
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        account_moq.OnSave += _ => is_saved = true;

        // User2 plan is restricted to realm 2 which has no active radius server
        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User2", "new-password");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("تغییر کلمه عبور VPN برای این سرور ممکن نیست");
        Assert.False(is_saved);
    }

    [Fact]
    public async Task ChangeVpnPassword_XuiRealm_RotatesUuidAndStoresTheEffectiveCredential()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User2", "live-uuid-2", 300, 500);

        AccountEntity? saved_account = null;
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().OnSave += account => saved_account = account;

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User2", "user-supplied-password");

        Assert.Equal(2, data.Code / 100);
        data.Message.Should().Contain("چرخش شناسه اتصال");

        // the returned (dispatcher-generated) credential is stored, never the input
        Assert.NotNull(saved_account);
        Guid.TryParse(saved_account.VpnPassword, out _).Should().BeTrue();
        saved_account.VpnPassword.Should().NotBe("user-supplied-password");
        saved_account.LastCredentialRotateTime.Should().NotBeNull();

        var rotated = api_moq.Clients.Single();
        rotated.Uuid.Should().Be(saved_account.VpnPassword);
        api_moq.Counters["User2"].Should().Be((300, 500), "rotation must not reset the counters");
    }

    [Fact]
    public async Task ChangeVpnPassword_XuiRealmWithoutPanelClient_FailsLoud()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);

        var is_saved = false;
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().OnSave += _ => is_saved = true;

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User2", "any");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("شناسه‌ی اتصال شما روی سرور یافت نشد");
        Assert.False(is_saved);
    }

    [Fact]
    public async Task ChangeVpnPassword_XuiRealmThrottledInsideTheWindow_Blocked()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().Data["User2"].LastCredentialRotateTime =
            DateTime.Now.AddMinutes(-10);

        var is_saved = false;
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().OnSave += _ => is_saved = true;

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User2", "any");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should()
            .Contain("چرخش شناسه اتصال تا 50 دقیقه‌ی دیگر ممکن نیست");
        Assert.False(is_saved);
    }

    [Fact]
    public async Task ChangeVpnPassword_XuiRealmThrottleExpired_Passes()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().Data["User2"].LastCredentialRotateTime =
            DateTime.Now.AddMinutes(-61);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User2", "live-uuid-2", 0, 0);

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User2", "");

        Assert.Equal(2, data.Code / 100);
    }

    [Fact]
    public async Task ChangeVpnPassword_XuiRealmThrottleDisabled_PassesImmediately()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);
        scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementOptions>>().Value
            .MinUuidRotationMinutes = 0;
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().Data["User2"].LastCredentialRotateTime =
            DateTime.Now.AddMinutes(-1);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User2", "live-uuid-2", 0, 0);

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User2", "");

        Assert.Equal(2, data.Code / 100);
    }

    [Fact]
    public async Task SendCertEmail_InvlaidAccount()
    {
        using var scope = App.Services.CreateScope();
        var vpn_app = scope.ServiceProvider.GetRequiredService<IVpnApplication>();
        var func = () => vpn_app.SendCertEmail("Invalid Username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task SendCertEmail_DisabledAccount()
    {
        using var scope = App.Services.CreateScope();
        var vpn_app = scope.ServiceProvider.GetRequiredService<IVpnApplication>();
        var func = () => vpn_app.SendCertEmail("InactiveUser7");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task SendCertEmail_WithoutEmail()
    {
        using var scope = App.Services.CreateScope();
        var vpn_app = scope.ServiceProvider.GetRequiredService<IVpnApplication>();
        var func = () => vpn_app.SendCertEmail("User8");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task SendCertEmail_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();
        var vpn_app = scope.ServiceProvider.GetRequiredService<IVpnApplication>();
        var func = () => vpn_app.SendCertEmail("User6");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task SendCertEmail()
    {
        using var scope = App.Services.CreateScope();

        var is_saved = false;
        var email_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        email_moq.OnSend += mail =>
        {
            is_saved = true;
            Assert.Equal("user2@gmail.com", mail.ToAddress);
            Assert.Single(mail.Attachments);
        };

        await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .SendCertEmail("User2");

        Assert.True(is_saved);
    }

    [Fact]
    public async Task SendCertEmail_RealmWithoutOvpn_BlockedBeforeCertificateBuild()
    {
        using var scope = App.Services.CreateScope();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        // realm 2 NAS without the OVPN capability bit
        server_moq
            .Setup(x => x.GetActiveNasInRealmOrAll(2))
            .ReturnsAsync([NewServer(21, 2, features: ServerFeature.Sstp)]);

        var email_sent = false;
        scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>().OnSend += _ => email_sent = true;

        var cert_touched = false;
        scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>().OnExecute += (_, _) => cert_touched = true;

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .SendCertEmail("User2");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("کانفیگ اتصال این سرور از نوع OpenVPN نیست");
        Assert.False(email_sent);
        Assert.False(cert_touched);
    }

    [Fact]
    public async Task SendCertEmail_OvpnNasWithUnimplementedOs_BlockedBeforeCertificateBuild()
    {
        using var scope = App.Services.CreateScope();
        var server_moq = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        // OVPN bit is set but the certificate method for Ubuntu NAS is not implemented
        server_moq
            .Setup(x => x.GetActiveNasInRealmOrAll(2))
            .ReturnsAsync([NewServer(21, 2, features: ServerFeature.Nas | ServerFeature.Ovpn, os_type: OperatingSystem.Ubuntu)]);

        var email_sent = false;
        scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>().OnSend += _ => email_sent = true;

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .SendCertEmail("User2");

        await func.Should().ThrowAsync<UserException>();
        Assert.False(email_sent);
    }

    [Fact]
    public async Task DownloadVpnConfig_WithoutEmailAddress_Succeeds()
    {
        using var scope = App.Services.CreateScope();

        var plan_moq = scope.ServiceProvider.GetRequiredService<PlanStateRepositoryMoq>();

        // User8 has no registered email address; the download path must not require one
        plan_moq.Setup(x => x.GetPlanState(8))
            .ReturnsAsync(new PlanStateEntity
            {
                Id = 8,
                Username = "User8",
                Status = AccountStatus.Active,
                SimultaneousUser = 1,
                RestrictedRealmId = 2,
                TrafficLimit = 1073741824,
                TimeLimitInDays = 120,
                ExpirationDate = DateTime.Now.AddDays(111),
                TrafficLeft = 1073001004,
            });

        var file = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .DownloadVpnConfig("User8");

        file.FileName.Should().Be("User8.zip");
        file.ContentType.Should().Be("application/zip");

        using var archive = new ZipArchive(new MemoryStream(file.Content));
        archive.Entries.Select(e => e.FullName).Should().Contain(["config.ovpn", "connection-info.txt"]);
    }

    [Fact]
    public async Task ChangeVpnPassword_RadiusPath_DoesNotStampTheRotationClock()
    {
        using var scope = App.Services.CreateScope();

        AccountEntity? saved_account = null;
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().OnSave += account => saved_account = account;

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User99", "new-password");

        Assert.Equal(2, data.Code / 100);
        Assert.NotNull(saved_account);
        // the throttle clock is x-ui-only: a Radius password change must not arm it
        Assert.Null(saved_account.LastCredentialRotateTime);
    }

    [Fact]
    public async Task ChangeVpnPassword_RadiusPath_RecentRotationStampDoesNotBlock()
    {
        using var scope = App.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().Data["User99"].LastCredentialRotateTime =
            DateTime.Now;

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User99", "new-password");

        Assert.Equal(2, data.Code / 100);
    }

    [Fact]
    public async Task ChangeVpnPassword_MixedRealm_FailsLoudEndToEnd()
    {
        using var scope = App.Services.CreateScope();

        // realm 2 carrying both an x-ui and a UserManager radius server
        scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>()
            .Setup(x => x.GetActiveRadiusInRealmOrAll(2))
            .ReturnsAsync([
                NewServer(21, 2, ServerFeature.Xui | ServerFeature.V2ray),
                NewServer(22, 2, ServerFeature.UserManager),
            ]);

        var is_saved = false;
        scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>().OnSave += _ => is_saved = true;

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .ChangeVpnPassword("User2", "new-password");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("تغییر شناسه‌ی اتصال برای این سرور پشتیبانی نمی‌شود");
        Assert.False(is_saved);
    }

    [Fact]
    public async Task SendCertEmail_XuiRealm_SendsTheVlessLinkWithoutAttachment()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);

        // pure x-ui realm: NAS without the OVPN capability bit, so the
        // certificate section stays hidden (ServerFeature.Nas is a mask
        // that would imply Ovpn)
        scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>()
            .Setup(x => x.GetActiveNasInRealmOrAll(2))
            .ReturnsAsync([NewServer(31, 2, features: ServerFeature.Sstp)]);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User2", "live-uuid-2", 0, 0);

        var sent_body = "";
        var attachment_count = -1;
        scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>().OnSend += mail =>
        {
            sent_body = mail.HtmlBody ?? string.Empty;
            attachment_count = mail.Attachments.Count;
        };

        await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .SendCertEmail("User2");

        sent_body.Should().Contain("vless://live-uuid-2@");
        sent_body.Should().NotContain("OpenVPN");
        attachment_count.Should().Be(0);
    }

    [Fact]
    public async Task SendCertEmail_RealmWithBothCapabilities_SendsOneEmailWithLinkAndCertificate()
    {
        using var scope = App.Services.CreateScope();

        // x-ui radius server for the vless link while the default realm-2 NAS
        // still provides the OpenVPN certificate: both artifacts in ONE email
        SetupXuiRealm(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User2", "live-uuid-2", 0, 0);

        var send_count = 0;
        var sent_body = "";
        var attachment_count = -1;
        scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>().OnSend += mail =>
        {
            send_count++;
            sent_body = mail.HtmlBody ?? string.Empty;
            attachment_count = mail.Attachments.Count;
        };

        await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .SendCertEmail("User2");

        send_count.Should().Be(1);
        sent_body.Should().Contain("vless://live-uuid-2@");
        sent_body.Should().Contain("User key password");
        attachment_count.Should().Be(1);
    }

    [Fact]
    public async Task GetConfigInfo_XuiRealm_ReturnsTheLiveVlessLink()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User2", "live-uuid-2", 0, 0);

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .GetConfigInfo("User2");

        Assert.Equal(2, data.Code / 100);
        data.Data.Kind.Should().Be("xui");
        data.Data.VlessLink.Should().StartWith("vless://live-uuid-2@srv21.photon-bypass.com:443");
    }

    [Fact]
    public async Task GetConfigInfo_NonXuiRealm_ReturnsKindOnly()
    {
        using var scope = App.Services.CreateScope();

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .GetConfigInfo("User99");

        Assert.Equal(2, data.Code / 100);
        data.Data.Kind.Should().Be("usermanager");
        data.Data.VlessLink.Should().BeNull();
    }

    [Fact]
    public async Task DownloadVpnConfig_XuiRealm_ReturnsBrandedConnectionSheet()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);

        var api_moq = scope.ServiceProvider.GetRequiredService<XuiApiMoq>();
        api_moq.AddClient("User2", "live-uuid-2", 0, 0);

        var file = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .DownloadVpnConfig("User2");

        file.FileName.Should().Be("User2.txt");
        file.ContentType.Should().Be("text/plain; charset=utf-8");

        // UTF-8 without BOM, like every other text artifact the panel serves
        file.Content.Should().NotBeEmpty();
        file.Content[0].Should().NotBe(0xEF);

        var text = System.Text.Encoding.UTF8.GetString(file.Content);

        // branded header
        text.Should().Contain("Fast Bypass | مشخصات اتصال");

        // D2 RTL/LTR rule: Persian label on one line, value on the next,
        // sections separated by exactly one blank line
        text.Should().Contain("نام کاربری:\nUser2");
        text.Should().Contain("لینک اتصال (vless):\nvless://live-uuid-2@srv21.photon-bypass.com:443");
        text.Should().Contain("راهنمای اتصال:\nhttps://guide.example.com/vpn");
        text.Should().Contain("نصب‌کننده OpenVPN:\nhttps://example.com/ovpn-installer");
        text.Should().Contain("سایت:\nhttps://photon-bypass.com");
        text.Should().Contain("\n\n");

        // the rotation note is x-ui-only, the OVPN password section is absent
        text.Should().Contain("نکته:");
        text.Should().Contain("با هر چرخش شناسه اتصال، کانفیگ قبلی باطل می‌شود؛ کانفیگ جدید را از پنل کاربری دریافت کنید.");
        text.Should().NotContain("رمز گواهی");
    }

    [Fact]
    public async Task GetConfigInfo_XuiRealmWhenThePanelClientIsMissing_ReportsAFriendlyError()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);

        // the link fetch fails (no client on the panel) and must surface as a
        // friendly user-facing error, not a raw infrastructure exception
        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .GetConfigInfo("User2");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("دریافت کانفیگ اتصال از سرور در حال حاضر ممکن نیست");
    }

    [Fact]
    public async Task DownloadVpnConfig_XuiRealmWhenThePanelClientIsMissing_ReportsAFriendlyError()
    {
        using var scope = App.Services.CreateScope();

        SetupXuiRealm(scope);

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .DownloadVpnConfig("User2");

        (await func.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("دریافت کانفیگ اتصال از سرور در حال حاضر ممکن نیست");
    }

    [Fact]
    public async Task DownloadVpnConfig_OvpnRealm_ReturnsZipWithCertAndInfoText()
    {
        using var scope = App.Services.CreateScope();

        // capture the exact certificate bytes the same pipeline emails, so the
        // ZIP entry can be byte-compared against the live CertFile
        byte[]? emailed_cert = null;
        scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>().OnSend += mail =>
        {
            emailed_cert = mail.Attachments.Single().Content;
        };

        await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .SendCertEmail("User2");

        emailed_cert.Should().NotBeNull();

        var file = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .DownloadVpnConfig("User2");

        file.FileName.Should().Be("User2.zip");
        file.ContentType.Should().Be("application/zip");

        using var archive = new ZipArchive(new MemoryStream(file.Content));

        archive.Entries.Select(e => e.FullName).Should().BeEquivalentTo(["config.ovpn", "connection-info.txt"]);

        // the certificate travels inside the ZIP unchanged
        ReadZipEntry(archive, "config.ovpn").Should().Equal(emailed_cert!);

        var info_text = System.Text.Encoding.UTF8.GetString(ReadZipEntry(archive, "connection-info.txt"));

        info_text.Should().Contain("Fast Bypass | مشخصات اتصال");
        info_text.Should().Contain("نام کاربری:\nUser2");
        info_text.Should().Contain("رمز گواهی (User key password):\n");
        info_text.Should().Contain("راهنمای اتصال:\nhttps://guide.example.com/vpn");
        info_text.Should().Contain("نصب‌کننده OpenVPN:\nhttps://example.com/ovpn-installer");
        info_text.Should().Contain("سایت:\nhttps://photon-bypass.com");

        // OVPN accounts do not rotate: no vless section, no rotation note
        info_text.Should().NotContain("لینک اتصال");
        info_text.Should().NotContain("نکته:");

        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();
        var download_event = history_moq.Data.SingleOrDefault(h => h.Description == "کانفیگ اتصال دانلود شد.");
        download_event.Should().NotBeNull();
        download_event.Target.Should().Be(2);
    }

    private static byte[] ReadZipEntry(ZipArchive archive, string entry_name)
    {
        var entry = archive.GetEntry(entry_name)!;

        using var stream = entry.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    [Fact]
    public async Task GetConfigInfo_InvalidAccount()
    {
        using var scope = App.Services.CreateScope();

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .GetConfigInfo("Invalid Username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetConfigInfo_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .GetConfigInfo("User6");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task DownloadVpnConfig_InvalidAccount()
    {
        using var scope = App.Services.CreateScope();

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .DownloadVpnConfig("Invalid Username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task DownloadVpnConfig_WithoutPlan()
    {
        using var scope = App.Services.CreateScope();

        var func = () => scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .DownloadVpnConfig("User6");

        await func.Should().ThrowAsync<UserException>();
    }

    private static void SetupXuiRealm(IServiceScope scope)
    {
        scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>()
            .Setup(x => x.GetActiveRadiusInRealmOrAll(2))
            .ReturnsAsync([NewServer(21, 2, ServerFeature.Xui | ServerFeature.V2ray)]);
    }

    private static ServerEntity NewServer(int id, int realm_id, ServerFeature features,
        OperatingSystem os_type = OperatingSystem.Mikrotik)
    {
        return new ServerEntity
        {
            Id = id,
            IsActive = true,
            RealmId = realm_id,
            Name = $"Server{id}",
            IpAddress = $"10.0.1.{id}",
            DomainName = $"srv{id}.photon-bypass.com",
            BandWidth = 100,
            OsType = os_type,
            Features = features,
        };
    }

    [Fact]
    public async Task TrafficData_User1()
    {
        using var scope = App.Services.CreateScope();

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .TrafficData("User99");

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(Today.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }

    [Fact]
    public async Task TrafficData_User2()
    {
        using var scope = App.Services.CreateScope();

        var data = await scope.ServiceProvider.GetRequiredService<IVpnApplication>()
            .TrafficData("User2");

        Assert.NotNull(data.Data);
        Assert.Equal(30, data.Data.Labels.Length);

        var index = 0;
        foreach (var t in data.Data.Labels)
        {
            Assert.Equal(Today.AddDays(index--).ToPersianDayOfMonth(), t);
        }

        foreach (var t in data.Data.Collections)
        {
            Assert.Equal(data.Data.Labels.Length, t.Data.Length);
        }
    }
}