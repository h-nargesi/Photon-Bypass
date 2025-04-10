using Microsoft.AspNetCore.Mvc;
using PhotonBypass.Domain.Model;
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
}
