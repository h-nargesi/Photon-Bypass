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
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<ITrafficDataRepository> TrafficDataRepo,
    Lazy<IPlanStateRepository> PlanStateRepo,
    Lazy<INasRepository> NasRepo,
    Lazy<IAccountRadiusSyncService> AccountRadiusSrv,
    Lazy<IServerManagementService> ServerMngSrv,
    Lazy<ISessionRadiusSyncService> SessionRadiusSyncSrv,
    Lazy<IEmailService> EmailSrv,
    Lazy<IJobContext> JobContext)
    : IVpnApplication
{
    private const int MaxDateBefore = 30;

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
            plan.TimeLeft is { TotalMinutes: < 1 } ||
            plan.TrafficLeft is < 1)
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
        var min_date_time = DateTime.Now.AddDays(-MaxDateBefore);

        var list = await TrafficDataRepo.Value.Fetch(target, min_date_time);

        _ = SessionRadiusSyncSrv.Value.UpdateTrafficData(target, min_date_time);

        var result = ConvertToModel(list);

        return ApiResult<TrafficDataModel>.Success(result);
    }

    private static TrafficDataModel ConvertToModel(IEnumerable<TrafficDataEntity> data)
    {
        var dict_data = data.GroupBy(k => k.StartSession.Date)
            .ToDictionary(
                k => k.Key, 
                v => v.ToArray());

        var now = DateTime.Now.Date;
        var all_days = new string[MaxDateBefore]
            .Select((_, i) => now.AddDays(-i))
            .ToList();

        var labels = all_days.Select(x => x.ToPersianDayOfMonth())
            .ToArray();

        var upload = new List<int>();
        var download = new List<int>();
        var total = new List<int>();

        foreach (var day in all_days)
        {
            if (!dict_data.TryGetValue(day, out var record))
            {
                upload.Add(0);
                download.Add(0);
                total.Add(0);
                continue;
            }

            var data_in = (int)(record.Sum(x => x.DataIn) / StaticValues.BytesInMeg);
            var data_out = (int)(record.Sum(x => x.DataOut) / StaticValues.BytesInMeg);

            upload.Add(data_out);
            download.Add(data_in);
            total.Add(data_in + data_out);
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