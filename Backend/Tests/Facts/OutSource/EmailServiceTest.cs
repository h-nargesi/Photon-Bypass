using FluentAssertions;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;
using System.Net.Mail;

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
        email_handler_moq.OnSend += mail => bodies.Add(mail.Body);

        // Two different service instances share the static template cache: the
        // second send must reuse the cached template and still apply its own
        // placeholder values.
        await email_service_1.SendResetPasswordLink("User One", "one@test.com", "code-111");
        await email_service_2.SendResetPasswordLink("User Two", "two@test.com", "code-222");

        Assert.Equal(2, bodies.Count);
        Assert.Contains("code-111", bodies[0]);
        Assert.Contains("code-222", bodies[1]);
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
            sent_body = mail.Body;
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

        // no placeholder or conditional marker may leak into the delivered body
        Assert.DoesNotContain("{username}", sent_body);
        Assert.DoesNotContain("{link}", sent_body);
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
            sent_body = mail.Body;
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
            sent_bodies.Add(mail.Body);
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
}
