namespace PhotonBypass.Domain.Static;

public interface IPriceCalculator
{
    int CalculatePrice(int users, int days, int gigabytes);
}
