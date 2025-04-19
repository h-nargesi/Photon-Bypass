using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Result;
using System.Collections.Generic;

namespace PhotonBypass.Application.Vpn;

class VpnApplication(
    Lazy<IRadiusService> RadiusSrv,
    Lazy<IEmailService> EmailSrv,
    Lazy<ITrafficDataRepository> TrafficDataRepo,
    Lazy<IAccountRepository> AccountRepo)
    : IVpnApplication
{
    private const int MAX_DATE_BEFORE = 30;

    public async Task<ApiResult> ChangeOvpnPassword(string target, string password)
    {
        await RadiusSrv.Value.ChangeOvpnPassword(target, password);

        return ApiResult.Success("کلمه عبور Ovpn تغییر کرد");
    }

    public async Task<ApiResult> SendCertEmail(string target)
    {
        await EmailSrv.Value.SendCertEmail(target, null);

        return ApiResult.Success("کلمه عبور Ovpn تغییر کرد");
    }

    public async Task<ApiResult<TrafficDataModel>> TrafficData(string target)
    {
        var minDateTime = DateTime.Now.AddDays(-MAX_DATE_BEFORE);

        var list = await TrafficDataRepo.Value.Fetch(target, minDateTime);

        if (list.Count < MAX_DATE_BEFORE)
        {
            var firstEmptyDate = FindFirstEmptyDate(list, minDateTime) ?? DateTime.Now;
            var type = firstEmptyDate < DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek) ?
                TrafficDataRequestType.Monthly : TrafficDataRequestType.Weekly;

            var data = await RadiusSrv.Value.FetchTrafficData(firstEmptyDate, type);

            var new_data = Merge(ref list, data, minDateTime);

            if (new_data?.Count > 0)
            {
                int account_id;

                if (list.Count > 0) account_id = list[0].AccountId;
                else
                {
                    var account = await AccountRepo.Value.GetAccount(target)
                        ?? throw new Exception($"Account not found: {target}");
                    account_id = account.Id;
                }

                foreach (var record in new_data)
                    record.AccountId = account_id;

                _ = TrafficDataRepo.Value.BachSave(new_data);
            }
        }

        var result = ConvertToModel(list);

        return ApiResult<TrafficDataModel>.Success(result);
    }

    private static DateTime? FindFirstEmptyDate(IEnumerable<TrafficDataEntity> data, DateTime from)
    {
        foreach (var record in data)
        {
            if (record.Day != from) return from;
            from = from.AddDays(1);
        }

        return null;
    }

    private static List<TrafficDataEntity> Merge(ref IList<TrafficDataEntity> destination, IEnumerable<TrafficDataRadius> source, DateTime minDateTime)
    {
        var destination_dict = destination.ToDictionary(k => k.Day);
        var new_data = new List<TrafficDataEntity>();

        foreach (var record in source)
        {
            if (record.Day < minDateTime) continue;

            if (!destination_dict.ContainsKey(record.Day))
            {
                var traffic = new TrafficDataEntity
                {
                    Day = record.Day,
                    DataIn = record.DataIn,
                    DataOut = record.DataOut,
                };

                new_data.Add(traffic);
                destination_dict.Add(record.Day, traffic);
            }
        }

        destination = [.. destination_dict.Values.OrderBy(x => x.Day)];

        return new_data;
    }

    private TrafficDataModel ConvertToModel(IEnumerable<TrafficDataEntity> data)
    {
        throw new NotImplementedException();
    }
}
