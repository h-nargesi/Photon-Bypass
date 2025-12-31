namespace PhotonBypass.Application.Billing.Model;

public class NewInvoiceInfo
{
    public int Price { get; init; }

    public string? Action { get; init; }

    public string Descripttion { get; init; } = null!;
}
