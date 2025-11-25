using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Account.Business;

public static class RenewalBusiness
{
    public static double? GetTrafficLimitInGig(this RenewalEntity entity)
    {
        return entity.TrafficLimit.HasValue ? Math.Round(entity.TrafficLimit.Value / StaticValues.BytesInGig, 2) : null;
    }
}
