using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IVpnApplication
{
    Task<ApiResult> ChangeOvpnPassword(ChangeOvpnContext context);

    Task<ApiResult> SendCertEmail(string target);

    Task<ApiResult<TrafficDataModel>> TrafficData(string target);
}
