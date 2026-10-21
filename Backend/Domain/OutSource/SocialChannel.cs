namespace PhotonBypass.Domain.OutSource;

/// <summary>
/// The active social channel, selected at runtime through
/// <c>SocialMediaOptions:Channel</c> (the compile-time <c>#if SOCIAL</c> gates
/// are gone). Also the DI key under which each adapter registers itself.
/// </summary>
public enum SocialChannel
{
    None = 0,

    Telegram = 1,

    /// <summary>Reserved seam for the future WhatsApp adapter (docs/16 style decision record).</summary>
    WhatsApp = 2,
}
