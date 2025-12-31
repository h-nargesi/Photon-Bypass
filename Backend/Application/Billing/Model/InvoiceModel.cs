using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Application.Billing.Model;

public class InvoiceModel
{
    public int Code { get; set; }

    public BalanceStatus Status { get; set; }

    public InvoiceItemModel[] InvoiceItems { get; set; } = null!;

    public int Sum => InvoiceItems.Sum(i => i.Value);

    public float Discount { get; set; }

    public int TotalSum => (int)(Sum * (1 - Discount));
}
