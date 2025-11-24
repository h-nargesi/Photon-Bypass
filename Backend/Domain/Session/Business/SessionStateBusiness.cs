using PhotonBypass.Domain.Profile.Model;

namespace PhotonBypass.Domain.Profile.Business;

public static class SessionStateBusiness
{
    private const long BytesInGig = 1024 * 1024 * 1024;
    public const float AccountFinishingStatePercent = 0.1f;

    public static string GetRemainsTitle(this AccountStateEntity entity)
    {
        var result = string.Empty;

        if (entity.TrafficLeft.HasValue)
        {
            var left_gigs = entity.TrafficLeft.Value / BytesInGig;
            result += $" و {left_gigs} گیگ باقی مانده";
        }

        if (entity.TimeLeft.HasValue)
        {
            var left_days = entity.TimeLeft.Value.TotalDays;
            var left_hours = entity.TimeLeft.Value.Hours;

            if (left_days > 0)
            {
                result += $" و {left_days} روز";
            }

            if (left_hours > 0)
            {
                result += $" و {left_hours} ساعت";
            }
        }
        
        if (result.Length > 0) result = result[3..];

        return result;
    }
}
