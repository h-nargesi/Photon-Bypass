using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockOptions;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.OutSource;

public class EmailServiceTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task SendResetPasswordLink_TemplateCachedAcrossInstances()
    {
        using var scope_1 = App.Services.CreateScope();
        using var scope_2 = App.Services.CreateScope();

        var email_service_1 = scope_1.ServiceProvider.GetRequiredService<IEmailService>();
        var email_service_2 = scope_2.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope_1.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var bodies = new List<string>();
        email_handler_moq.OnSend += mail => bodies.Add(mail.HtmlBody!);

        // Two different service instances share the static template cache: the
        // second send must reuse the cached template and still apply its own
        // placeholder values.
        await email_service_1.SendResetPasswordLink("User One", "one@test.com", "code-111");
        await email_service_2.SendResetPasswordLink("User Two", "two@test.com", "code-222");

        Assert.Equal(2, bodies.Count);
        Assert.Contains("code-111", bodies[0]);
        Assert.Contains("code-222", bodies[1]);

        // footer branding is applied to every template
        Assert.Contains("Fast Bypass", bodies[0]);
        Assert.Contains("https://photon-bypass.com", bodies[0]);
        Assert.DoesNotContain("{title}", bodies[0]);
        Assert.DoesNotContain("{site_url}", bodies[0]);
    }

    [Fact]
    public async Task EveryEmail_CarriesAPlainTextViewOfTheSameContent()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        EmailMessage? sent = null;
        email_handler_moq.OnSend += mail => sent = mail;

        await email_service.SendResetPasswordLink("User One", "one@test.com", "code-111");

        sent.Should().NotBeNull();
        sent!.PlainTextBody.Should().NotBeNullOrEmpty("every email must ship a plain-text view");
        sent.PlainTextBody!.Should().Contain("code-111");
        sent.PlainTextBody.Should().Contain("https://photon-bypass.com");

        // no markup or conditional marker may leak into the plain-text body
        sent.PlainTextBody.Should().NotContain("<");
        sent.PlainTextBody.Should().NotContain("<!--if:");
        sent.PlainTextBody.Should().NotContain("<!--end:");
    }

    [Fact]
    public async Task UserValues_AreHtmlEncodedInHtmlOnly()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        EmailMessage? sent = null;
        email_handler_moq.OnSend += mail => sent = mail;

        await email_service.FinishServiceAlert(
            "<b>Ali</b> & Co", "user1", "one@test.com", "100 گیگی <و> دو روزه", "2 روز");

        sent.Should().NotBeNull();
        sent!.HtmlBody.Should().Contain("&lt;b&gt;Ali&lt;/b&gt; &amp; Co");
        sent.HtmlBody.Should().Contain("100 گیگی &lt;و&gt; دو روزه");
        sent.HtmlBody.Should().NotContain("<b>Ali</b>");

        // the plain-text view keeps the raw values
        sent.PlainTextBody.Should().Contain("<b>Ali</b> & Co");
    }

    [Fact]
    public async Task SendConnectionEmail_VlessOnly_RendersTheLinkAndStripsMissingSections()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var sent_to = "";
        var sent_body = "";
        var attachment_count = -1;
        email_handler_moq.OnSend += mail =>
        {
            sent_to = mail.ToAddress;
            sent_body = mail.HtmlBody!;
            attachment_count = mail.Attachments.Count;
        };

        await email_service.SendConnectionEmail("User Two", "two@test.com", new ConnectionEmailContext
        {
            Username = "User2",
            Link = "vless://4c1f3b2a-1111-2222-3333-444455556666@vm06.ir.parssafe:443?type=ws",
        });

        Assert.Equal("two@test.com", sent_to);
        Assert.Contains("User2", sent_body);
        Assert.Contains("vless://4c1f3b2a-1111-2222-3333-444455556666@vm06.ir.parssafe:443?type=ws", sent_body);

        // sections the account does not own are stripped from the merged template
        Assert.DoesNotContain("Password:", sent_body);
        Assert.DoesNotContain("OpenVPN", sent_body);
        Assert.DoesNotContain("نصب‌کننده OpenVPN", sent_body);

        // guide branding replaces the hardcoded link
        Assert.Contains("href=\"https://guide.example.com/vpn\"", sent_body);

        // no placeholder or conditional marker may leak into the delivered body
        Assert.DoesNotContain("{username}", sent_body);
        Assert.DoesNotContain("{link}", sent_body);
        Assert.DoesNotContain("{guide_url}", sent_body);
        Assert.DoesNotContain("<!--if:", sent_body);
        Assert.DoesNotContain("<!--end:", sent_body);

        // the x-ui delivery carries the link in the body: no OVPN attachment
        Assert.Equal(0, attachment_count);
    }

    [Fact]
    public async Task SendConnectionEmail_CertificateOnly_AttachesOvpnAndStripsTheVlessSection()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var sent_body = "";
        var attachment_names = new List<string>();
        email_handler_moq.OnSend += mail =>
        {
            sent_body = mail.HtmlBody!;
            attachment_names = [.. mail.Attachments.Select(a => a.FileName)];
        };

        await email_service.SendConnectionEmail("User One", "one@test.com", new ConnectionEmailContext
        {
            Username = "User2",
            Password = "vpn-pass-123",
            PrivateKeyOvpn = "key-pass-456",
            CertFile = [1, 2, 3],
        });

        Assert.Contains("User2", sent_body);
        Assert.Contains("vpn-pass-123", sent_body);
        Assert.Contains("key-pass-456", sent_body);
        Assert.Contains("فایل config.ovpn در پیوست قرار دارد.", sent_body);
        Assert.Contains("نصب‌کننده OpenVPN", sent_body);
        Assert.Contains("href=\"https://example.com/ovpn-installer\"", sent_body);

        // the account has no vless link: the whole section is hidden
        Assert.DoesNotContain("vless", sent_body);
        Assert.DoesNotContain("چرخش شناسه‌ی اتصال", sent_body);
        Assert.DoesNotContain("<!--if:", sent_body);

        attachment_names.Should().Equal("cert.ovpn");
    }

    [Fact]
    public async Task SendConnectionEmail_BothKinds_RendersAllSectionsInOneEmail()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var sent_bodies = new List<string>();
        var send_count = 0;
        email_handler_moq.OnSend += mail =>
        {
            sent_bodies.Add(mail.HtmlBody!);
            send_count++;
        };

        await email_service.SendConnectionEmail("User Two", "two@test.com", new ConnectionEmailContext
        {
            Username = "User2",
            Password = "vpn-pass-123",
            Link = "vless://4c1f3b2a-1111-2222-3333-444455556666@vm06.ir.parssafe:443?type=ws",
            PrivateKeyOvpn = "key-pass-456",
            CertFile = [1, 2, 3],
        });

        // one request, one email, every owned artifact inside
        Assert.Equal(1, send_count);
        var body = sent_bodies.Single();
        Assert.Contains("vless://4c1f3b2a-1111-2222-3333-444455556666@vm06.ir.parssafe:443?type=ws", body);
        Assert.Contains("vpn-pass-123", body);
        Assert.Contains("key-pass-456", body);
    }

    [Fact]
    public async Task EveryEmail_CarriesTheLogoAsEmbeddedCidResource()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var logo_found = false;
        var cid_body = "";
        email_handler_moq.OnSend += mail =>
        {
            cid_body = mail.HtmlBody!;

            mail.EmbeddedLogo.Should().NotBeNull("the logo must travel on the message");
            mail.EmbeddedLogo!.Length.Should().BeGreaterThan(0);
            mail.LogoContentType.Should().Be("image/png");

            logo_found = true;
        };

        await email_service.SendResetPasswordLink("User One", "one@test.com", "code-111");

        Assert.True(logo_found);
        Assert.Contains("src=\"cid:logo\"", cid_body);
        Assert.DoesNotContain("via.placeholder.com", cid_body);
    }

    [Fact]
    public async Task SendConnectionEmail_EmptyInstallerUrl_StripsTheInstallerLine()
    {
        using var scope = App.Services.CreateScope();

        var branding = scope.ServiceProvider.GetRequiredService<IOptions<BrandingOptions>>();
        var configured_url = branding.Value.OvpnInstallerUrl;
        branding.Value.OvpnInstallerUrl = null;

        try
        {
            var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

            var sent_body = "";
            email_handler_moq.OnSend += mail => sent_body = mail.HtmlBody!;

            await email_service.SendConnectionEmail("User One", "one@test.com", new ConnectionEmailContext
            {
                Username = "User2",
                Password = "vpn-pass-123",
                PrivateKeyOvpn = "key-pass-456",
                CertFile = [1, 2, 3],
            });

            // the certificate section survives, its installer line does not
            Assert.Contains("فایل config.ovpn در پیوست قرار دارد.", sent_body);
            Assert.DoesNotContain("نصب‌کننده OpenVPN", sent_body);
            Assert.DoesNotContain("{ovpn_installer_url}", sent_body);
        }
        finally
        {
            branding.Value.OvpnInstallerUrl = configured_url;
        }
    }

    [Fact]
    public async Task MissingLogoFile_FailsFastWithClearMessage()
    {
        using var scope = App.Services.CreateScope();

        var branding = scope.ServiceProvider.GetRequiredService<IOptions<BrandingOptions>>();
        var configured_path = branding.Value.LogoPath;
        branding.Value.LogoPath = "Branding/missing-logo.png";

        try
        {
            var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var func = () => email_service.SendResetPasswordLink("User One", "one@test.com", "code-111");

            (await func.Should().ThrowAsync<FileNotFoundException>())
                .Which.Message.Should().Contain("Branding/missing-logo.png");
        }
        finally
        {
            branding.Value.LogoPath = configured_path;
        }
    }

    [Fact]
    public async Task FaultedLogoRead_IsEvictedFromTheCache()
    {
        using var scope = App.Services.CreateScope();

        var branding = scope.ServiceProvider.GetRequiredService<IOptions<BrandingOptions>>();
        var configured_path = branding.Value.LogoPath;
        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var send_count = 0;
        email_handler_moq.OnSend += _ => send_count++;

        // same cache key on both attempts: the logo file is created between
        // them, proving the faulted task was evicted (docs/14 M36) instead of
        // staying cached until a restart
        var temp_logo = Path.Combine(Path.GetTempPath(), $"pb-logo-{Guid.NewGuid():N}.png");
        branding.Value.LogoPath = temp_logo;

        try
        {
            await FluentActions.Awaiting(() => email_service.SendResetPasswordLink("User One", "one@test.com", "code-111"))
                .Should().ThrowAsync<FileNotFoundException>();

            File.Copy(Path.Combine(AppContext.BaseDirectory, "Branding", "logo.png"), temp_logo);

            await email_service.SendResetPasswordLink("User One", "one@test.com", "code-222");

            send_count.Should().Be(1);
        }
        finally
        {
            branding.Value.LogoPath = configured_path;
            File.Delete(temp_logo);
        }
    }

    [Fact]
    public async Task SendConnectionEmail_PlainTextView_StripsMissingSections()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var sent_text = "";
        email_handler_moq.OnSend += mail => sent_text = mail.PlainTextBody ?? string.Empty;

        await email_service.SendConnectionEmail("User Two", "two@test.com", new ConnectionEmailContext
        {
            Username = "User2",
            Link = "vless://4c1f3b2a-1111-2222-3333-444455556666@vm06.ir.parssafe:443?type=ws",
        });

        // the plain-text view carries the same conditional rendering: the
        // password and OpenVPN sections never reach the recipient
        sent_text.Should().Contain("User2");
        sent_text.Should().Contain("vless://4c1f3b2a-1111-2222-3333-444455556666@vm06.ir.parssafe:443?type=ws");
        sent_text.Should().NotContain("Password:");
        sent_text.Should().NotContain("OpenVPN");
        sent_text.Should().NotContain("<!--if:");
        sent_text.Should().NotContain("<!--end:");
    }

    [Fact]
    public async Task TemplatesAndLogo_ResolveFromTheApplicationBaseDirectory()
    {
        using var scope = App.Services.CreateScope();

        // templates and branding resolve from the app base directory (the
        // test output folder here), never from the process working directory
        File.Exists(Path.Combine(AppContext.BaseDirectory, "EmailTemplates", "ResetPassword.html"))
            .Should().BeTrue("the template folder must resolve from the app base");
        File.Exists(Path.Combine(AppContext.BaseDirectory, "Branding", "logo.png"))
            .Should().BeTrue("the branding logo must resolve from the app base");
    }

    [Fact]
    public async Task RootedLogoPath_IsUsedAsIs()
    {
        using var scope = App.Services.CreateScope();

        var branding = scope.ServiceProvider.GetRequiredService<IOptions<BrandingOptions>>();
        var configured_path = branding.Value.LogoPath;

        var rooted_path = Path.Combine(AppContext.BaseDirectory, "Branding", "logo.png");
        branding.Value.LogoPath = rooted_path;

        try
        {
            var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

            var logo_found = false;
            email_handler_moq.OnSend += mail => logo_found = mail.EmbeddedLogo is { Length: > 0 };

            await email_service.SendResetPasswordLink("User One", "one@test.com", "code-111");

            logo_found.Should().BeTrue("an absolute logo path must be honored directly");
        }
        finally
        {
            branding.Value.LogoPath = configured_path;
        }
    }

    [Fact]
    public async Task SendWelcomeEmail_RendersTemplateAndBranding()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        EmailMessage? sent = null;
        email_handler_moq.OnSend += mail => sent = mail;

        await email_service.SendWelcomeEmail("User One", "one@test.com", "user1");

        sent.Should().NotBeNull();
        sent!.ToAddress.Should().Be("one@test.com");
        sent.Subject.Should().Contain("خوش آمدید");
        sent.HtmlBody.Should().Contain("User One");
        sent.HtmlBody.Should().Contain("user1");
        sent.HtmlBody.Should().Contain("https://guide.example.com/vpn");
        sent.HtmlBody.Should().Contain("https://photon-bypass.com");
        sent.HtmlBody.Should().NotContain("{fullname}");
        sent.PlainTextBody.Should().Contain("user1");
    }

    [Fact]
    public async Task SendRenewalSuccessEmail_RendersPlanPriceAndExpiration()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        EmailMessage? sent = null;
        email_handler_moq.OnSend += mail => sent = mail;

        await email_service.SendRenewalSuccessEmail("User One", "one@test.com", "50 گیگی و 120 روزه و 1 کاربره", 250000, "1405/07/01");

        sent.Should().NotBeNull();
        sent!.ToAddress.Should().Be("one@test.com");
        sent.Subject.Should().Contain("تمدید");
        sent.HtmlBody.Should().Contain("50 گیگی و 120 روزه و 1 کاربره");
        sent.HtmlBody.Should().Contain("250,000");
        sent.HtmlBody.Should().Contain("1405/07/01");
        sent.HtmlBody.Should().Contain("https://photon-bypass.com");
        sent.HtmlBody.Should().NotContain("{plan_title}");
        sent.PlainTextBody.Should().Contain("250,000");
    }
}
