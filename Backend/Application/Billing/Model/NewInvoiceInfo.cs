namespace PhotonBypass.Application.Billing.Model;

public class NewInvoiceInfo
{
    public int Price { get; init; }

    public int Needs { get; init; }

    public string Action { get; init; } = null!;

    public string Descripttion { get; init; } = null!;
}
