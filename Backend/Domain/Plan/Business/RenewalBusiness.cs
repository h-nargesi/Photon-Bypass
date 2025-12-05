using System.Text;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Domain.Plan.Business;

public static class RenewalBusiness
{
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

        result.Append($" و {entity.SimultaneousUser} کاربره");

        if (result.Length > 0) result.Remove(0, 3);

        return result.ToString();
    }

    public static double? GetTrafficLimitInGig(this RenewalEntity entity)
    {
        return entity.TrafficLimit.HasValue ? Math.Round(entity.TrafficLimit.Value / StaticValues.BytesInGigDouble, 2) : null;
    }

    public static bool RenewalValidation(this RenewalEntity validation, out UserException error)
    {
        if (validation.TrafficLimit is null or < 1)
        {
            error = new UserException(
                "نمی‌توانید پلن بدون ترافیک ثبت نمایید!",
                "Invalid renewal traffic=null");
            return true;
        }

        if (validation.TrafficLimit / StaticValues.BytesInGigDouble % 25 != 0)
        {
            error = new UserException(
                "ترافیک باید ضریبی از ۲۵ باشد.",
                $"Invalid renewal traffic={validation.TrafficLimit}, gigabytes={validation.TrafficLimit / StaticValues.BytesInGigDouble}");
            return true;
        }

        error = null!;
        return false;
    }
}