using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Entity;

[Table("Invoice")]
public class InvoiceEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Code { get; set; }

    [NotMapped]
    public int Id { get => Code; set => Code = value; }

    public int AccountId { get; set; }

    public InvoiceKind Kind { get; set; }

    public string Title { get; set; } = null!;

    public int TotalPrice { get; set; }

    public int WalletDeduction { get; set; }

    public int Payable { get; set; }

    public string? Action { get; set; }

    public BalanceStatus Status { get; set; } = BalanceStatus.Pending;

    public byte[]? ReceiptImage { get; set; }

    public string? ReceiptText { get; set; }

    public DateTime? ReceiptAt { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;

    [NotMapped]
    public bool HasReceipt => ReceiptImage != null || ReceiptText != null;
}
