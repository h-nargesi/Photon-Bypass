using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Domain.Model.Auth;
using PhotonBypass.Domain.Model.Basic;
using PhotonBypass.Domain.Model.Connection;
using PhotonBypass.Domain.Model.Plan;
using PhotonBypass.Domain.Model.User;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IApplication
{
    Task<ApiResult<IEnumerable<PriceModel>>> GetPrices();


    Task<ApiResult<FullUserModel>> CheckUserPassword(TokenContext context);


    Task<ApiResult> ChangeOvpnPassword(ChangeOvpnContext context);

    Task<ApiResult> SendCertEmail(SendCertEmailContext context);

    Task<ApiResult<TrafficDataModel>> TrafficData(TrafficDataContext context);

    Task<ApiResult<FullUserModel>> GetFullInfo(FullInfoContext context);

    Task<ApiResult> EditUser(EditUserModel model);

    Task<ApiResult<HistoryModel[]>> GetHistory(HistoryContext context);


    Task<ApiResult<int[]>> GetCurrentConnectionState(CurrentConnectionStateContext context);

    Task<ApiResult> CloseConnection(CloseConnectionContext context);


    Task<ApiResult<UserPlanInfo>> GetPlanState(PlanStateContext context);

    Task<ApiResult<PlanInfo>> GetPlanInfo(PlanInfoContext context);

    Task<ApiResult<long>> Estimate(RnewalContext context);

    Task<ApiResult<RnewalResult>> Rnewal(RnewalContext context);
}
