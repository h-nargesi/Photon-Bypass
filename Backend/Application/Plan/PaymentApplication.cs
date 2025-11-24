using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

class PaymentApplication : IPaymentApplication
{
    public Task<ApiResult> PaymentCallback(string token)
    {
        throw new NotImplementedException();
    }
}
