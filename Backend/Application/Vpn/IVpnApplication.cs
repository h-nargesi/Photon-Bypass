using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Vpn;

public interface IVpnApplication
{
    /// <summary>The visible VPN accounts of the request target's area (docs/22 §3.5).</summary>
    Task<ApiResult<List<VpnAccountSummaryModel>>> GetAccounts(string? target);

    Task<ApiResult> ChangeVpnPassword(int account_id, string password);

    Task<ApiResult> SendCertEmail(int account_id);

    Task<ApiResult<VpnConfigInfoModel>> GetConfigInfo(int account_id);

    Task<VpnConfigFileModel> DownloadVpnConfig(int account_id);

    Task<ApiResult<TrafficDataModel>> TrafficData(int account_id);
}
