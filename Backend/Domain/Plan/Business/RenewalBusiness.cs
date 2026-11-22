using System.Text;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Domain.Plan.Business;

public static class RenewalBusiness
{
    public const int UpdateTrafficDataTimeSecondLimit = 300;

    public static string GetPlanTitle(this IRenewalEntity entity)
    {
        var result = new StringBuilder();

        if (entity.TrafficLimit.HasValue)
        {
            result.Append($" و {(int)(entity.TrafficLimit / StaticValues.BytesInGigDouble)} گیگی");
        }

        if (entity.TimeLimitInDays.HasValue)
        {
            result.Append($" و {entity.TimeLimitInDays} روزه");
        }

        // Docs/24 §2: null count = unlimited
        result.Append(entity.SimultaneousUser.HasValue
            ? $" و {entity.SimultaneousUser} کاربره"
            : " و کاربر نامحدود");

        if (result.Length > 0) result.Remove(0, 3);

        return result.ToString();
    }

    public static double? GetTrafficLimitInGig(this RenewalEntity entity)
    {
        return entity.TrafficLimit.HasValue ? Math.Round(entity.TrafficLimit.Value / StaticValues.BytesInGigDouble, 2) : null;
    }

    /// <summary>
    /// Docs/24 §6: a renewal must keep at least one of the four limitations —
    /// an all-unlimited purchase is refused (mirrors the old "no plan without
    /// traffic and expiry" rule, now across all axes).
    /// </summary>
    public static bool HasNoLimit(byte? users, short? days, long? traffic_bytes, int? rate, out UserException error)
    {
        if (users.HasValue || days.HasValue || traffic_bytes.HasValue || rate.HasValue)
        {
            error = null!;
            return false;
        }

        error = new UserException("پلن بدون هیچ محدودیتی قابل خریداری نیست!",
            "Composed renewal has no limitation on any axis (Docs/24 §6).");
        return true;
    }
}
