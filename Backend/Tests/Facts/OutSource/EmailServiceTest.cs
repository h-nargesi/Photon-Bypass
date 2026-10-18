using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockOptions;
using PhotonBypass.Test.Mock.MockServerBridge;
using System.Net.Mail;
using System.Text;

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
        email_handler_moq.OnSend += mail => bodies.Add(HtmlBody(mail));

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
    public async Task SendConnectionEmail_VlessOnly_RendersTheLinkAndStripsMissingSections()
    {
        using var scope = App.Services.CreateScope();

        var email_service = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        // capture inside the callback: the service disposes the MailMessage after sending
        var sent_to = "";
        var sent_body = "";
        var attachment_count = -1;
        email_handler_moq.OnSend += mail =>
        {
            sent_to = mail.To.First().Address;
            sent_body = HtmlBody(mail);
            attachment_count = mail.Attachments?.Count ?? 0;
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
            sent_body = HtmlBody(mail);
            attachment_names = [.. mail.Attachments.Select(a => a.Name!)];
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
            sent_bodies.Add(HtmlBody(mail));
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
            // the body travels through the HTML alternate view only
            mail.AlternateViews.Count.Should().Be(1);
            mail.Body.Should().BeEmpty("the body must ride on the alternate view, not the plain-text part");

            cid_body = HtmlBody(mail);

            var logo = mail.AlternateViews[0].LinkedResources.SingleOrDefault(r => r.ContentId == "logo");

            logo.Should().NotBeNull("the logo must be embedded as a CID resource");
            logo!.ContentType.MediaType.Should().Be("image/png");

            using var copy = new MemoryStream();
            logo.ContentStream.CopyTo(copy);
            copy.Length.Should().BeGreaterThan(0);

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
            email_handler_moq.OnSend += mail => sent_body = HtmlBody(mail);

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

    /// <summary>
    /// The delivered HTML lives on the message's alternate view, not in
    /// <see cref="MailMessage.Body"/>; read it back from the view's content stream.
    /// </summary>
    private static string HtmlBody(MailMessage mail)
    {
        var view = mail.AlternateViews[0];

        using var reader = new StreamReader(view.ContentStream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
