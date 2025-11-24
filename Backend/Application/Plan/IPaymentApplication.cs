using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPaymentApplication
{
    Task<ApiResult> PaymentCallback(string token);
}
