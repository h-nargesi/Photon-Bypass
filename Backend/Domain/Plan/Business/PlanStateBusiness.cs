using System.Text;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan.Business;

public static class PlanStateBusiness
{
    private const float AccountFinishingStatePercent = 0.1f;

    public static bool IsFinishing(this PlanStateEntity entity)
    {
        return entity.TimeLeftPercent < AccountFinishingStatePercent ||
               entity.TrafficLeftPercent < AccountFinishingStatePercent;
    }

    public static string? GetRemainsTitle(this PlanStateEntity entity)
    {
        var result = new StringBuilder();

        if (entity.TrafficLeft.HasValue)
        {
            result.Append($" و {entity.GetTrafficLeftInGig()} گیگ باقی مانده");
        }

        if (entity.TimeLeft.HasValue)
        {
            var left_days = entity.TimeLeft.Value.TotalDays;
            var left_hours = entity.TimeLeft.Value.Hours;

            if (left_days > 0)
            {
                result.Append($" و {left_days} روز");
            }

            if (left_hours > 0)
            {
                result.Append($" و {left_hours} ساعت");
            }
        }

        return result.Length > 0 ? result.Remove(0, 3).ToString() : null;
    }

    public static double? GetTrafficLimitInGig(this PlanStateEntity entity)
    {
        return entity.TrafficLimit.HasValue ? Math.Round(entity.TrafficLimit.Value / StaticValues.BytesInGigDouble, 2) : null;
    }

    public static double GetTrafficUsedInGig(this PlanStateEntity entity)
    {
        return Math.Round(entity.TrafficUsed / StaticValues.BytesInGigDouble, 2);
    }

    public static double? GetTrafficLeftInGig(this PlanStateEntity entity)
    {
        return entity.TrafficLeft.HasValue ? Math.Round(entity.TrafficLeft.Value / StaticValues.BytesInGigDouble, 2) : null;
    }
}