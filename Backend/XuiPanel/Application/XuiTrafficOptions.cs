using System.ComponentModel.DataAnnotations;

namespace PhotonBypass.Xui.Application;

public class XuiTrafficOptions
{
    public const int DefaultIntervalSeconds = 300;

    public const int DefaultLockThresholdSeconds = 240;

    [Range(60, 86_400)]
    public int IntervalSeconds { get; set; } = DefaultIntervalSeconds;

    [Range(30, 3_600)]
    public int LockThresholdSeconds { get; set; } = DefaultLockThresholdSeconds;
}
