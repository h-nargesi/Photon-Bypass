using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan.Business;

public static class RenewalBusiness
{
    public static string GetTitle(this RenewalEntity entity)
    {
        var result = string.Empty;

        if (entity.TrafficLimit.HasValue)
        {
            result += $" و {(int)(entity.TrafficLimit / StaticValues.BytesInGig)} گیگ";
        }

        if (entity.MonthLimit.HasValue)
        {
            result += $" و {entity.MonthLimit} ماه";
        }

        if (result.Length > 0) result = result[3..];

        return result;
    }

    public static double? GetTrafficLimitInGig(this RenewalEntity entity)
    {
        return entity.TrafficLimit.HasValue ? Math.Round(entity.TrafficLimit.Value / StaticValues.BytesInGig, 2) : null;
    }

    public static bool RenewalValidation(this RenewalEntity validation, out string user_message)
    {
        if (validation.TrafficLimit == null)
        {
            throw new Exception("Invalid renewal traffic=null");
        }

        if (validation.TrafficLimit / StaticValues.BytesInGig % 25 != 0)
        {
            throw new Exception($"Invalid renewal traffic={validation.TrafficLimit}, gigabytes={validation.TrafficLimit / StaticValues.BytesInGig}");
        }

        user_message = string.Empty;
        return false;
    }
}
