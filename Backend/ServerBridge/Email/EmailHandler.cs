using Microsoft.Extensions.Options;
using MimeKit;
using PhotonBypass.ServerBridge.Services;
using Serilog;

namespace PhotonBypass.ServerBridge.Email;

class EmailHandler(IOptions<EmailOptions> options, EmailSendLimiter send_limiter) : IEmailHandler
{
    public EmailOptions Options => options.Value;

    public async Task Send(EmailMessage message)
    {
        if (!options.Value.Enabled)
        {
            // counted as delivered: callers must not build an hourly retry
            // loop around a feature that is simply switched off (dev default)
            Log.Information("Email disabled; dropping send to {0}", message.ToAddress);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Value.Address))
        {
            throw new InvalidOperationException("EmailOptions: Address is not set");
        }

        if (string.IsNullOrWhiteSpace(options.Value.Password))
        {
            throw new InvalidOperationException("EmailOptions: Password is not set");
        }

        // budget before any network I/O: an exhausted cap is a fast typed
        // failure — the hourly warning job retries unstamped accounts anyway
        if (!send_limiter.TryReserve())
        {
            throw new InvalidOperationException(
                $"EmailOptions:MaxSendsPerHour ({send_limiter.Cap}) exceeded — send to {message.ToAddress} rejected; the hourly warning job retries unstamped accounts.");
        }

        var mime = Build(message, options.Value);

        using var smtp = new MailKit.Net.Smtp.SmtpClient();
        using var send_cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SendTimeoutSeconds));

        try
        {
            await smtp.ConnectAsync(options.Value.Host, options.Value.Port,
                options.Value.EnableSsl ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable : MailKit.Security.SecureSocketOptions.None,
                send_cancellation.Token);
            await smtp.AuthenticateAsync(options.Value.Address!, options.Value.Password!, send_cancellation.Token);
            await smtp.SendAsync(mime, send_cancellation.Token);

            Log.Information("Email sent: to={0}, subject={1}", message.ToAddress, message.Subject);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Email send timed out after {options.Value.SendTimeoutSeconds}s.");
        }
        finally
        {
            if (smtp.IsConnected)
            {
                // QUIT is local and fast: not worth letting a burnt timeout
                // token skip the graceful disconnect
                await smtp.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Maps the neutral DTO onto the MIME tree:
    /// multipart/mixed → [ multipart/related → ( multipart/alternative →
    /// text/plain + text/html, logo with <see cref="EmailMessage.LogoContentId"/> ),
    /// attachments ]. Static and side-effect free so tests can verify the
    /// structure without any network access.
    /// </summary>
    internal static MimeMessage Build(EmailMessage message, EmailOptions options)
    {
        var mime = new MimeMessage
        {
            Subject = message.Subject,
        };

        mime.From.Add(new MailboxAddress(options.FullName, options.Address));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));

        var alternative = new Multipart("alternative");

        if (message.PlainTextBody != null)
        {
            alternative.Add(new TextPart("plain") { Text = message.PlainTextBody });
        }

        if (message.HtmlBody != null)
        {
            alternative.Add(new TextPart("html") { Text = message.HtmlBody });
        }

        var related = new Multipart("related")
        {
            alternative,
        };

        if (message.EmbeddedLogo is { Length: > 0 })
        {
            var (logo_type, logo_subtype) = SplitMediaType(message.LogoContentType);

            related.Add(new MimePart(logo_type, logo_subtype)
            {
                ContentId = EmailMessage.LogoContentId,
                Content = new MimeContent(new MemoryStream(message.EmbeddedLogo)),
                ContentTransferEncoding = ContentEncoding.Base64,
            });
        }

        var mixed = new Multipart("mixed")
        {
            related,
        };

        foreach (var attachment in message.Attachments)
        {
            var (type, subtype) = SplitMediaType(attachment.ContentType);

            mixed.Add(new MimePart(type, subtype)
            {
                FileName = attachment.FileName,
                Content = new MimeContent(new MemoryStream(attachment.Content)),
                ContentTransferEncoding = ContentEncoding.Base64,
            });
        }

        mime.Body = mixed;

        return mime;
    }

    private static (string Type, string Subtype) SplitMediaType(string content_type)
    {
        var separator = content_type.IndexOf('/');

        return separator < 0
            ? (content_type, "")
            : (content_type[..separator], content_type[(separator + 1)..]);
    }
}
