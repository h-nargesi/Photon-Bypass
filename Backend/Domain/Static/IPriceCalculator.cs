using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Static;

public interface IPriceCalculator
{
    int CalculatePrice(int users, int value);
}
