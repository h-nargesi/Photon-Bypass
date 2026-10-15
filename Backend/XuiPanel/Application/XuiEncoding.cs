using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Xui.Application;

/// <summary>
/// Field encodings for the x-ui client payload (docs/16 D13, docs/17):
/// per-renewal enforcement pushes a full new quota with a counter reset,
/// expiry is "now + days" and null means "never" (encoding pending the vm01 probe).
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
            TotalTrafficBytes = renewal.TrafficLimit,
            ExpiryTime = ToExpiryTime(renewal.TimeLimitInDays),
            LimitIp = renewal.SimultaneousUser,
        };
    }

    public static long? ToExpiryTime(short? time_limit_in_days)
    {
        return time_limit_in_days.HasValue
            ? new DateTimeOffset(DateTime.Now.AddDays(time_limit_in_days.Value)).ToUnixTimeMilliseconds()
            : null;
    }
}
