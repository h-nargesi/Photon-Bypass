using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Vpn;

public interface IVpnApplication
{
    Task<ApiResult> ChangeVpnPassword(string target, string password);

    Task<ApiResult> SendCertEmail(string target);

    Task<CertContext> DownloadCertificate(string target);

    Task<ApiResult<VpnConfigInfoModel>> GetConfigInfo(string target);

    Task<VpnConfigFileModel> DownloadVpnConfig(string target);

    Task<ApiResult<TrafficDataModel>> TrafficData(string target);
}
