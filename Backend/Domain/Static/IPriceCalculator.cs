namespace PhotonBypass.Domain.Static;

public interface IPriceCalculator
{
    Task<int> CalculatePrice(int price_id, int users, int days, int gigabytes);
}
