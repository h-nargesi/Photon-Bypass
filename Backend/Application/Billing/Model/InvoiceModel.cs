using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Application.Billing.Model;

public class InvoiceModel
{
    public int Code { get; set; }

    public InvoiceKind Kind { get; set; }

    public BalanceStatus Status { get; set; }

    public int TotalPrice { get; set; }

    public int WalletDeduction { get; set; }

    public int Payable { get; set; }

    public int WalletBalance { get; set; }

    public bool AllowWallet { get; set; }

    public bool HasReceipt { get; set; }

    public InvoiceItemModel[] Items { get; set; } = [];

    public PaymentCard[] CardInfo { get; set; } = [];
}
