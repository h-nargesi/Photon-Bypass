namespace PhotonBypass.ServerBridge.Services;

/// <summary>
/// Transport-neutral email DTO: the application layer describes what to send
/// without touching MimeKit, so the MIME dependency stays inside ServerBridge.
/// The From mailbox is derived from <see cref="Email.EmailOptions"/> by the
/// handler at send time.
/// </summary>
public sealed class EmailMessage
{
    /// <summary>ContentId of the embedded branding logo in every template.</summary>
    public const string LogoContentId = "logo";

    public string ToName { get; init; } = "";

    public string ToAddress { get; init; } = "";

    public string Subject { get; init; } = "";

    public string? HtmlBody { get; init; }

    public string? PlainTextBody { get; init; }

    public byte[]? EmbeddedLogo { get; init; }

    public string LogoContentType { get; init; } = "image/png";

    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
}

public sealed class EmailAttachment(byte[] content, string fileName, string contentType)
{
    public byte[] Content { get; } = content;

    public string FileName { get; } = fileName;

    public string ContentType { get; } = contentType;
}
