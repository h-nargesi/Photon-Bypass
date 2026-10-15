using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Xui.Application;

/// <summary>
/// Field encodings for the x-ui client payload (docs/16 D13, docs/17):
/// per-renewal enforcement pushes a full new quota with a counter reset.
/// Contract code-verified from alireza0/x-ui v1.12.0 and v1.14.0 source: `totalGB` is
/// bytes (0 = unlimited), `expiryTime` is a unix-ms epoch where 0 = never,
/// `limitIp` maps SimultaneousUser.
/// </summary>
public static class XuiEncoding
{
    public static XuiClientModel BuildClient(string email, string uuid, RenewalEntity renewal)
    {
        return new XuiClientModel
        {
            Email = email,
            Uuid = uuid,
            Enable = true,
            TotalTrafficBytes = renewal.TrafficLimit ?? 0,
            ExpiryTime = ToExpiryTime(renewal.TimeLimitInDays),
            LimitIp = renewal.SimultaneousUser,
        };
    }

    public static long ToExpiryTime(short? time_limit_in_days)
    {
        return time_limit_in_days.HasValue
            ? new DateTimeOffset(DateTime.Now.AddDays(time_limit_in_days.Value)).ToUnixTimeMilliseconds()
            : 0;
    }
}
