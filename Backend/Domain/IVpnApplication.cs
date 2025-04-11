using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IVpnApplication
{
    Task<ApiResult> ChangeOvpnPassword(ChangeOvpnContext context);

    Task<ApiResult> SendCertEmail(SendCertEmailContext context);

    Task<ApiResult<TrafficDataModel>> TrafficData(TrafficDataContext context);
}
