using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

public interface IPaymentAppliocation
{
    Task<ApiResult> PaymentCallback(string token);
}
