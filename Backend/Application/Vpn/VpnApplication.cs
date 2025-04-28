using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;

namespace PhotonBypass.Application.Vpn;

class VpnApplication(
    Lazy<IRadiusService> RadiusSrv,
    Lazy<IEmailService> EmailSrv,
    Lazy<ITrafficDataRepository> TrafficDataRepo,
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IVpnNodeService> VpnNodeSrv,
    Lazy<IPermanentUsersRepository> UserRepo,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<IRealmRepository> RealmRepo,
    Lazy<IUserPlanStateRepository> PlanStateRepo,
    Lazy<IJobContext> JobContext)
    : IVpnApplication
{
    private const int MAX_DATE_BEFORE = 30;
    private const int BYTES_IN_MEGABYTES = 1024 * 1024;

    public async Task<ApiResult> ChangeOvpnPassword(string target, string password)
    {
        var account = await AccountRepo.Value.GetAccount(target) ??
            throw new UserException("کاربر پیدا نشد!", $"target: {target}");

        var result = await RadiusSrv.Value.ChangeOvpnPassword(account.PermanentUserId, password);

        if (!result)
        {
            return new ApiResult
            {
                Code = 500,
                Message = "تغییر کلمه عبور Ovpn با خطا مواجه شد!",
            };
        }

        _ = HistoryRepo.Value.Save(new HistoryEntity
        {
            Issuer = JobContext.Value.Username,
            Target = target,
            EventTime = DateTime.Now,
            Title = "امنیت",
            Description = "تغییر کلمه عبور ovpn.",
        });

        return ApiResult.Success("کلمه عبور Ovpn تغییر کرد.");
    }

    public async Task<ApiResult> SendCertEmail(string target)
    {
        var user = await UserRepo.Value.GetUser(target) ??
            throw new UserException("کاربر پیدا نشد!", $"target: {target}");

        if (!user.Active)
        {
            throw new UserException("کاربر پیدا نشد!", $"user is inactive: target={target}");
        }

        var server_task = PlanStateRepo.Value.GetRestrictedServer(user.Id);

        var ovpn_password_task = RadiusSrv.Value.GetOvpnPassword(user.Id);

        var server = await server_task;

        if (server == null)
        {
            var realm = await RealmRepo.Value.Fetch(user.RealmId);
            server = realm?.RestrictedServerIP;
        }

        CertContext cert_context;

        if (server != null)
        {
            cert_context = await VpnNodeSrv.Value.GetCertificate(server);
        }
        else
        {
            cert_context = await ServerMngSrv.Value.GetDefaultCertificate(user.Realm);
        }

        var email_context = new CertEmailContext
        {
            Username = user.Username,
            Password = (await ovpn_password_task) ?? throw new Exception($"Password not found for user: {target}"),
            Server = cert_context.Server,
            PrivateKey = cert_context.PrivateKey,
            CertFile = cert_context.CertFile,
        };

        await EmailSrv.Value.SendCertEmail(target, email_context);

        _ = HistoryRepo.Value.Save(new HistoryEntity
        {
            Issuer = JobContext.Value.Username,
            Target = target,
            EventTime = DateTime.Now,
            Title = "امنیت",
            Description = "ایمیل گواهی اتصال ارسال شد.",
        });

        return ApiResult.Success("ایمیل گواهی اتصال ارسال شد.");
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

            var data = await RadiusSrv.Value.FetchTrafficData(target, firstEmptyDate, type);

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
            if (record.Day < minDateTime || record.Day >= DateTime.Now)
            {
                continue;
            }

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

    private static TrafficDataModel ConvertToModel(IEnumerable<TrafficDataEntity> data)
    {
        data = data.OrderBy(x => x.Day);

        var labels = data.Select(x => x.Day.ToPersianString("dd"))
            .ToArray();

        var upload = new List<int>();
        var download = new List<int>();
        var total = new List<int>();

        foreach (var record in data)
        {
            var D = (int)(record.DataIn / BYTES_IN_MEGABYTES);
            var U = (int)(record.DataOut / BYTES_IN_MEGABYTES);

            upload.Add(U);
            download.Add(D);
            total.Add(D + U);
        }

        return new TrafficDataModel
        {
            Title = "ترافیک یک ماه گذشته",
            Collections =
            [
                new TrafficRecordModel
                {
                    Title = "Download",
                    Data = [.. download],
                },
                new TrafficRecordModel
                {
                    Title = "Upload",
                    Data = [.. upload],
                },
                new TrafficRecordModel
                {
                    Title = "Download",
                    Data = [.. total],
                }
            ],
            Labels = labels,
        };
    }
}
