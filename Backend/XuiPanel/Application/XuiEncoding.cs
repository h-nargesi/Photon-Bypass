using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Xui.Application;

/// <summary>
/// Field encodings for the x-ui client payload (docs/16 D13, docs/17 D16):
/// each enforcement push carries the stacked quota with a counter reset.
/// Contract code-verified from alireza0/x-ui v1.12.0 and v1.14.0 source:
/// `totalGB` is bytes (0 = unlimited), `expiryTime` is a unix-ms epoch where
/// 0 = never, `limitIp` maps SimultaneousUser.
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
            // D16: the pushed total is the stacked remaining (cumulative
            // remaining at purchase + this renewal's share), not the new
            // share alone. Rows written before D16 (stacked column NULL with
            // a limited TrafficLimit) fall back to their own share; 0 = unlimited.
            TotalTrafficBytes = renewal.CumulativeTrafficLeft ?? renewal.TrafficLimit ?? 0,
            ExpiryTime = ToExpiryTime(renewal),
            LimitIp = renewal.SimultaneousUser,
        };
    }

    public static long ToExpiryTime(RenewalEntity renewal)
    {
        var expiry = renewal.ExpirationDate
                     ?? (renewal.TimeLimitInDays.HasValue
                         ? (DateTime?)DateTime.Now.AddDays(renewal.TimeLimitInDays.Value)
                         : null);

        return expiry.HasValue ? new DateTimeOffset(expiry.Value).ToUnixTimeMilliseconds() : 0;
    }
}
