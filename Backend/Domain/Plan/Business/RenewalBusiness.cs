using System.Text;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
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

        result.Append($" و {entity.SimultaneousUser} کاربره");

        if (result.Length > 0) result.Remove(0, 3);

        return result.ToString();
    }

    public static double? GetTrafficLimitInGig(this RenewalEntity entity)
    {
        return entity.TrafficLimit.HasValue ? Math.Round(entity.TrafficLimit.Value / StaticValues.BytesInGigDouble, 2) : null;
    }

    public static bool RenewalValidation(this RenewalEntity validation, AccountEntity account, out UserException error)
    {
        if (account.UserType.HasFlag(UserTypes.AllowMonthly))
        {
            if (validation.TimeLimitInDays is null or < 1 or > 180 && validation.TrafficLimit is null or < 1 or > 300)
            {
                error = new UserException(
                    "نمی‌توانید پلن بدون ترافیک و تاریخ انقضا ثبت نمایید!",
                    $"Invalid renewal time-limit={validation.TimeLimitInDays} traffic={validation.TrafficLimit}");
                return true;
            }
        }
        else if (validation.TrafficLimit is null or < 1 or > 300)
        {
            error = new UserException(
                "نمی‌توانید پلن بدون ترافیک ثبت نمایید!",
                $"Invalid renewal account-type={UserTypes.AllowMonthly} traffic={validation.TrafficLimit}");
            return true;
        }

        if (validation.TrafficLimit != null)
        {
            if (validation.TrafficLimit / StaticValues.BytesInGigDouble % 25 != 0)
            {
                error = new UserException(
                    "ترافیک باید ضریبی از ۲۵ باشد.",
                    $"Invalid renewal traffic={validation.TrafficLimit}, gigabytes={validation.TrafficLimit / StaticValues.BytesInGigDouble}");
                return true;
            }
            
            var gigabyte_packages = validation.TrafficLimit.Value / StaticValues.BytesInGigLong / 25;
            var days_limit = 30 + 2.5 * gigabyte_packages - 0.004 * Math.Pow(gigabyte_packages, 2);
            validation.TimeLimitInDays = (short)(days_limit / ((validation.SimultaneousUser + 1) / 2));
        }
        else if (validation.TimeLimitInDays % 30 != 0)
        {
            error = new UserException(
                "زمان پلن باید ضریبی از ۳۰ باشد.",
                $"Invalid renewal time-in-day={validation.TimeLimitInDays}");
            return true;
        }

        error = null!;
        return false;
    }
}