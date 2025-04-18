using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Vpn;

public interface IVpnApplication
{
    Task<ApiResult> ChangeOvpnPassword(ChangeOvpnContext context);

    Task<ApiResult> SendCertEmail(string target);

    Task<ApiResult<TrafficDataModel>> TrafficData(string target);
}
