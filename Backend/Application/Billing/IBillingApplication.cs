using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Billing;

public interface IBillingApplication
{
    Task<ApiResult<string?>> GenerateInvoiceCode(NewInvoiceInfo? new_info);

    Task<ApiResult<InvoiceModel?>> GetInvoice(string? code);

    Task<ApiResult<BalanceStatus>> PaymentCallback(string token);
}
