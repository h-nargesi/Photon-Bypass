using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Static;

public interface IPriceCalculator
{
    int CalculatePrice(PlanType type, int users, int value);
}
