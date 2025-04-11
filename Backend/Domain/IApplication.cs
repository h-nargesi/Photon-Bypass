using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain.Model.Account;
using PhotonBypass.Domain.Model.Basic;
using PhotonBypass.Domain.Model.Connection;
using PhotonBypass.Domain.Model.Plan;
using PhotonBypass.Domain.Model.User;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IApplication
{
    ApiResult<IEnumerable<PriceModel>> GetPrices();


    ApiResult ChangeOvpnPassword(ChangeOvpnContext context);

    ApiResult SendCertEmail(SendCertEmailContext context);

    ApiResult<TrafficDataModel> TrafficData(TrafficDataContext context);

    ApiResult<FullUserModel> GetFullInfo(FullInfoContext context);

    ApiResult EditUser(EditUserModel model);

    ApiResult<HistoryModel[]> GetHistory(HistoryContext context);


    ApiResult<int[]> GetCurrentConnectionState(CurrentConnectionStateContext context);

    ApiResult CloseConnection(CloseConnectionContext context);


    ApiResult<UserPlanInfo> GetPlanState(PlanStateContext context);

    ApiResult<PlanInfo> GetPlanInfo(PlanInfoContext context);

    ApiResult<long> Estimate(RnewalContext context);

    ApiResult<RnewalResult> Rnewal(RnewalContext context);
}
