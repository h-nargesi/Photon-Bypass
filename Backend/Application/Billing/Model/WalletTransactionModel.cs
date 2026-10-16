using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Tools;
using System.Text.Json.Serialization;

namespace PhotonBypass.Application.Billing.Model;

public class WalletTransactionModel
{
    public int Id { get; set; }

    public int Amount { get; set; }

    public BalanceDirection Direction { get; set; }

    public BalanceStatus Status { get; set; }

    public string Description { get; set; } = null!;

    public int? InvoiceCode { get; set; }

    /// <summary>Username of the admin who issued a manual adjustment (docs/20 D5); null on system rows.</summary>
    public string? Actor { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime EventTime { get; set; }

    public string EventTimeTitle { get; set; } = null!;
}
