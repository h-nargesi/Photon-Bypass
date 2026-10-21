using PhotonBypass.Domain.OutSource;

namespace PhotonBypass.Domain.OutSource.Model;

/// <summary>
/// Runtime channel selection for <see cref="ISocialMediaService"/>. Default is
/// <see cref="SocialChannel.None"/> (the no-op adapter), so a fresh install
/// starts without any social dependency configured.
/// </summary>
public class SocialMediaOptions
{
    public SocialChannel Channel { get; set; } = SocialChannel.None;
}
