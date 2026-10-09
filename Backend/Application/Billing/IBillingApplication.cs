using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Billing;

public interface IBillingApplication
{
    Task<ApiResult<int?>> IssueTopUp(int value);

    Task<ApiResult<int?>> IssuePlanInvoice(int estimate, string action, string title);

    Task<ApiResult<InvoiceModel?>> GetInvoice(int code);

    Task<ApiResult> RegisterReceipt(int code, byte[]? image, string? file_name, string? content_type, string? text);

    Task<ApiResult> SettleWallet(int code);
}
