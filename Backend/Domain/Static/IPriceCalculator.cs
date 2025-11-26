namespace PhotonBypass.Domain.Static;

public interface IPriceCalculator
{
    int CalculatePrice(int price_id, int users, int days, int gigabytes);

    Task UpdateCalculatorCode();
}
