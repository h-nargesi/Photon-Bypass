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
            if (validation.TimeLimitInDays is null or < 1 or > 180 && validation.TrafficLimit is null or < 1 or > 300 * StaticValues.BytesInGigLong)
            {
                error = new UserException(
                    "نمی‌توانید پلن بدون ترافیک و تاریخ انقضا ثبت نمایید!",
                    $"Invalid renewal account-type={account.UserType}, time-limit={validation.TimeLimitInDays}, traffic={validation.TrafficLimit}");
                return true;
            }
        }
        else if (validation.TrafficLimit is null or < 1 or > 300 * StaticValues.BytesInGigLong)
        {
            error = new UserException(
                "نمی‌توانید پلن بدون ترافیک ثبت نمایید!",
                $"Invalid renewal account-type={account.UserType}, traffic={validation.TrafficLimit}");
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
            var traffic_bonus = 60 + gigabyte_packages * 30;
            var user_fine = 5 * UserFine(validation.SimultaneousUser);
            validation.TimeLimitInDays = (short)(traffic_bonus - user_fine);
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
    
    private static int UserFine(int users)
    {
        if (users <= 1) return 0;
        else if (users >= 5) return users + 3;
        else return (int)(4.5 * users - 3 - Math.Pow(users, 2) / 2);
    }
}