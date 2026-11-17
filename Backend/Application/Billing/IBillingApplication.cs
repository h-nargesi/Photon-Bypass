using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Billing;

public interface IBillingApplication
{
    Task<ApiResult<int?>> IssueTopUp(int value);

    /// <summary>
    /// Issues a plan invoice; <paramref name="vpn_account_id"/> anchors it to
    /// the renewed account (Docs/22 §2), null on new-account invoices that
    /// only resolve at settlement.
    /// </summary>
    Task<ApiResult<int?>> IssuePlanInvoice(int estimate, string action, string title, int? vpn_account_id = null);

    Task<ApiResult<InvoiceModel?>> GetInvoice(int code);

    Task<ApiResult> RegisterReceipt(int code, byte[]? image, string? file_name, string? content_type, string? text);

    Task<ApiResult> SettleWallet(int code);
}
