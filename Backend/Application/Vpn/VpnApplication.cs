using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;

namespace PhotonBypass.Application.Vpn;

class VpnApplication(
    Lazy<IAccountRadiusSyncService> AccountRadiusSrv,
    Lazy<IEmailService> EmailSrv,
    Lazy<ITrafficDataRepository> TrafficDataRepo,
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<IRealmRepository> RealmRepo,
    Lazy<IRenewalRepository> RenewalRepo,
    Lazy<IPlanStateRepository> PlanStateRepo,
    Lazy<INasRepository> NasRepo,
    Lazy<IJobContext> JobContext)
    : IVpnApplication
{
    private const int MAX_DATE_BEFORE = 30;
    private const int BYTES_IN_MEGABYTES = 1024 * 1024;

    public async Task<ApiResult> ChangeVpnPassword(string target, string password)
    {
        var account = (await AccountRepo.Value.GetAccount(target)) ??
                      throw new UserException("کاربر پیدا نشد!", $"target not found: {target}");

        if (!account.Active)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        var result = await AccountRadiusSrv.Value.ChangeVpnPassword(account.Username, password);

        if (!result)
        {
            return new ApiResult
            {
                Code = 500,
                Message = "تغییر کلمه عبور VPN با خطا مواجه شد!",
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
        var account = (await AccountRepo.Value.GetAccount(target)) ??
            throw new UserException("کاربر پیدا نشد!", $"target not found: {target}");

        if (!account.Active)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={account.Username}");
        }

        if (account.Email == null)
        {
            throw new UserException("ایمیل کاربر ثبت نشده است!", $"account is email address is unkown: target={target}");
        }

        var plan = await PlanStateRepo.Value.GetPlanState(account.Id);

        if (plan == null || 
            plan.TimeLeft.HasValue && plan.TimeLeft.Value.TotalMinutes < 1 ||
            plan.TrafficLeft.HasValue && plan.TrafficLeft.Value < 1)
        {
            throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                                    $"There is not ant plan for user ");
        }

        var vpn_password_task = AccountRadiusSrv.Value.GetVpnPassword(account.Username);

        var cert_context = await ServerMngSrv.Value.GetDefaultCertificate(plan.RestrictedRealmId);

        if (plan?.RestrictedRealmId != null)
        {
            var servers = await NasRepo.Value.GetAllActiveInRealm(plan.RestrictedRealmId.Value) ??
                throw new Exception($"There is not any nas for realm: {plan.RestrictedRealmId.Value}.");

            await AccountRadiusSrv.Value.GetCertificate(servers, account.Username, cert_context);
        }

        var email_context = new CertEmailContext
        {
            Username = account.Username,
            Password = (await vpn_password_task) ?? throw new Exception($"Password not found for user: {target}"),
            Realm = cert_context.Realm,
            PrivateKeyOvpn = cert_context.PrivateKeyOvpn,
            CertFile = cert_context.CertFile,
        };

        await EmailSrv.Value.SendCertEmail(account.Fullname, account.Email, email_context);

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
            var type = firstEmptyDate < DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek)
                ? TrafficDataRequestType.Monthly
                : TrafficDataRequestType.Weekly;

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
        return data.OrderByDescending(x => x.Day)
            .Where(x => x.Day >= from)
            .Select(x => (DateTime?)x.Day)
            .FirstOrDefault()?
            .Date.AddDays(1);
    }

    private static List<TrafficDataEntity> Merge(ref List<TrafficDataEntity> destination,
        IEnumerable<TrafficDataRadius> source, DateTime minDateTime)
    {
        var destination_dict = destination.ToDictionary(k => k.Day);
        var new_data = new List<TrafficDataEntity>();

        foreach (var record in source)
        {
            if (record.Day < minDateTime || record.Day >= DateTime.Now)
            {
                continue;
            }

            if (destination_dict.TryGetValue(record.Day, out var data))
            {
                if (data.DataIn != record.DataIn || data.DataOut != record.DataOut)
                {
                    data.DataOut = record.DataOut;
                    data.DataIn = record.DataIn;
                    new_data.Add(data);
                }
            }
            else
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
        var dictData = data.ToDictionary(x => x.Day.Date);

        var now = DateTime.Now.Date;
        var alldays = new string[MAX_DATE_BEFORE]
            .Select((_, i) => now.AddDays(-i))
            .ToList();

        var labels = alldays.Select(x => x.ToPersianDayOfMonth())
            .ToArray();

        var upload = new List<int>();
        var download = new List<int>();
        var total = new List<int>();

        foreach (var day in alldays)
        {
            if (!dictData.TryGetValue(day, out var record))
            {
                record = TrafficDataEntity.Empty;
            }

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