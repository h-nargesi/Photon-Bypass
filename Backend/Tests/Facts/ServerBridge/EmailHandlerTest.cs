using FluentAssertions;
using Microsoft.Extensions.Options;
using MimeKit;
using PhotonBypass.ServerBridge.Email;
using PhotonBypass.ServerBridge.Services;

namespace PhotonBypass.Test.Facts.ServerBridge;

public class EmailHandlerTest
{
    [Fact]
    public void Build_ProducesTheMixedRelatedAlternativeStructure()
    {
        var message = new EmailMessage
        {
            ToName = "Recipient",
            ToAddress = "to@example.com",
            Subject = "subject",
            HtmlBody = "<p>body</p>",
            PlainTextBody = "body",
            EmbeddedLogo = [1, 2, 3],
            LogoContentType = "image/png",
            Attachments = [new EmailAttachment([4, 5, 6], "cert.ovpn", "application/octet-stream")],
        };

        var options = new EmailOptions
        {
            Enabled = true,
            FullName = "Sender Name",
            Address = "from@example.com",
        };

        var mime = EmailHandler.Build(message, options);

        mime.Subject.Should().Be("subject");

        var from = mime.From.OfType<MailboxAddress>().Single();
        from.Name.Should().Be("Sender Name");
        from.Address.Should().Be("from@example.com");

        var to = mime.To.OfType<MailboxAddress>().Single();
        to.Name.Should().Be("Recipient");
        to.Address.Should().Be("to@example.com");

        // multipart/mixed → [ multipart/related → ( multipart/alternative →
        // text/plain + text/html, logo ), attachment ]
        var mixed = mime.Body.Should().BeOfType<Multipart>().Subject;
        mixed.ContentType.MediaSubtype.Should().Be("mixed");
        mixed.Count.Should().Be(2);

        var related = mixed.OfType<Multipart>().Single();
        related.ContentType.MediaSubtype.Should().Be("related");

        var alternative = related.OfType<Multipart>().Single();
        alternative.ContentType.MediaSubtype.Should().Be("alternative");

        var plain = alternative.OfType<TextPart>().Single(part => part.IsPlain);
        plain.Text.Should().Be("body");

        var html = alternative.OfType<TextPart>().Single(part => part.IsHtml);
        html.Text.Should().Be("<p>body</p>");

        var logo = related.OfType<MimePart>().Single();
        logo.ContentId.Should().Be(EmailMessage.LogoContentId);
        logo.ContentType.MediaType.Should().Be("image");
        logo.ContentType.MediaSubtype.Should().Be("png");

        var attachment = mixed.OfType<MimePart>().Single();
        attachment.FileName.Should().Be("cert.ovpn");
        attachment.ContentType.MediaType.Should().Be("application");
        attachment.ContentType.MediaSubtype.Should().Be("octet-stream");
    }

    [Fact]
    public async Task Send_Disabled_DropsTheMessageWithoutNetworkAccess()
    {
        var handler = new EmailHandler(Options.Create(new EmailOptions { Enabled = false }), new EmailSendLimiter(0));

        // must complete without ever opening an SMTP connection
        await handler.Send(new EmailMessage { ToAddress = "to@example.com", Subject = "s" });
    }

    [Fact]
    public void Send_EnabledWithoutAddress_ThrowsTypedConfigurationError()
    {
        var handler = new EmailHandler(Options.Create(new EmailOptions
        {
            Enabled = true,
            Address = null,
            Password = "secret",
        }), new EmailSendLimiter(0));

        var function = () => handler.Send(new EmailMessage { ToAddress = "to@example.com" });

        function.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Address*");
    }

    [Fact]
    public void Send_EnabledWithoutPassword_ThrowsTypedConfigurationError()
    {
        var handler = new EmailHandler(Options.Create(new EmailOptions
        {
            Enabled = true,
            Address = "from@example.com",
            Password = null,
        }), new EmailSendLimiter(0));

        var function = () => handler.Send(new EmailMessage { ToAddress = "to@example.com" });

        // regression: this used to report "Address is not set" (copy-paste)
        function.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Password*");
    }

    [Fact]
    public async Task Send_ExhaustedHourlyCap_ThrowsTypedErrorWithoutNetworkIo()
    {
        // attempts count, not deliveries: a plain reserve spends the budget
        var limiter = new EmailSendLimiter(1);
        limiter.TryReserve().Should().BeTrue();

        var handler = new EmailHandler(Options.Create(new EmailOptions
        {
            Enabled = true,
            Address = "from@example.com",
            Password = "secret",
        }), limiter);

        var function = () => handler.Send(new EmailMessage { ToAddress = "to@example.com" });

        // the reserve sits before Build/Connect, so the rejection is a fast
        // typed failure — no SMTP connection is ever attempted
        (await function.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*MaxSendsPerHour*");
    }

    [Fact]
    public async Task Send_Disabled_ConsumesNoHourlyBudget()
    {
        var limiter = new EmailSendLimiter(1);

        var handler = new EmailHandler(Options.Create(new EmailOptions { Enabled = false }), limiter);

        await handler.Send(new EmailMessage { ToAddress = "to@example.com" });

        limiter.TryReserve().Should().BeTrue("a disabled no-op must not spend the shared budget");
    }
}

public class EmailOptionsValidationTest
{
    [Fact]
    public void Disabled_IsValidEvenWhenEmpty()
    {
        EmailOptionsValidation.IsValid(new EmailOptions()).Should().BeTrue();
    }

    [Fact]
    public void Enabled_RequiresAddressAndPasswordAndHost()
    {
        EmailOptionsValidation.IsValid(new EmailOptions
        {
            Enabled = true,
            Address = "from@example.com",
            Password = "secret",
            Host = "smtp.example.com",
        }).Should().BeTrue();

        EmailOptionsValidation.IsValid(new EmailOptions { Enabled = true, Address = null, Password = "secret" })
            .Should().BeFalse();

        EmailOptionsValidation.IsValid(new EmailOptions { Enabled = true, Address = "from@example.com", Password = " " })
            .Should().BeFalse();

        EmailOptionsValidation.IsValid(new EmailOptions { Enabled = true, Address = "from@example.com", Password = "secret", Host = "" })
            .Should().BeFalse();
    }
}
