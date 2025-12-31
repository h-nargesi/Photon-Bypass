using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Billing;

public interface IBillingApplication
{
    Task<ApiResult<int?>> GenerateInvoiceCode(int value);

    Task<ApiResult<int?>> GenerateInvoiceCode(NewInvoiceInfo? new_info = null);

    Task<ApiResult<InvoiceModel?>> GetInvoice(int? code);

    Task<ApiResult<BalanceStatus>> PaymentCallback(string token);
}
