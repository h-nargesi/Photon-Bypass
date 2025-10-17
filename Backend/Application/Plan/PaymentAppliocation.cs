using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

class PaymentAppliocation : IPaymentAppliocation
{
    public Task<ApiResult> PaymentCallback(string token)
    {
        throw new NotImplementedException();
    }
}
