using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Account.Business;

public static class SessionStateBusiness
{
    public const float AccountFinishingStatePercent = 0.1f;

    public static string GetRemainsTitle(this SessionStateEntity entity)
    {
        var result = string.Empty;

        if (entity.TrafficLeft.HasValue)
        {
            result += $" و {entity.GetTrafficLeftInGig()} گیگ باقی مانده";
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

    public static double? GetTrafficLimitInGig(this SessionStateEntity entity)
    {
        return entity.TrafficLimit.HasValue ? Math.Round(entity.TrafficLimit.Value / StaticValues.BytesInGig, 2) : null;
    }

    public static double GetTrafficUsedInGig(this SessionStateEntity entity)
    {
        return Math.Round(entity.TrafficUsed / StaticValues.BytesInGig, 2);
    }

    public static double? GetTrafficLeftInGig(this SessionStateEntity entity)
    {
        return entity.TrafficLeft.HasValue ? Math.Round(entity.TrafficLeft.Value / StaticValues.BytesInGig, 2) : null;
    }
}
