using System.ComponentModel.DataAnnotations;

namespace PhotonBypass.Portal.Basical.Captcha;

/// <summary>
/// Self-hosted adaptive captcha (docs plan: self-hosted-captcha-auth).
/// Captcha is not required by default; after <see cref="FailureThreshold"/>
/// failed anonymous auth attempts from one IP inside the failure window the
/// gate becomes mandatory for that IP only.
/// </summary>
public class CaptchaOptions
{
    [Range(4, 8)]
    public int CodeLength { get; set; } = 5;

    [Range(30, 600)]
    public int CodeExpirySeconds { get; set; } = 180;

    [Range(1, 100)]
    public int FailureThreshold { get; set; } = 3;

    [Range(1, 1440)]
    public int FailureWindowMinutes { get; set; } = 15;

    [Range(120, 400)]
    public int ImageWidth { get; set; } = 200;

    [Range(40, 200)]
    public int ImageHeight { get; set; } = 70;

    public bool Enabled { get; set; } = true;
}
