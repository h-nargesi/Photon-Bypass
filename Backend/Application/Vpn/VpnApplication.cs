using Microsoft.Extensions.Options;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Management.Model;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using System.Text;

namespace PhotonBypass.Application.Vpn;

class VpnApplication(
    Lazy<IAccountRepository> account_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<ITrafficDataRepository> traffic_data_repo,
    Lazy<IPlanStateRepository> plan_state_repo,
    Lazy<IPlanUsageReader> plan_usage_reader,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IServerManagementService> server_mng_srv,
    Lazy<IAccountApplication> account_app,
    Lazy<IEmailService> email_srv,
    Lazy<IVpnLinkService> vpn_link_srv,
    Lazy<IJobContext> job_context,
    IOptions<ManagementOptions> options)
    : IVpnApplication
{
    private const int MaxDateBefore = 30;

    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<ITrafficDataRepository> TrafficDataRepo { get; } = traffic_data_repo;
    private Lazy<IPlanStateRepository> PlanStateRepo { get; } = plan_state_repo;
    private Lazy<IPlanUsageReader> PlanUsageReader { get; } = plan_usage_reader;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IServerManagementService> ServerMngSrv { get; } = server_mng_srv;
    private Lazy<IAccountApplication> AccountApp { get; } = account_app;
    private Lazy<IEmailService> EmailSrv { get; } = email_srv;
    private Lazy<IVpnLinkService> VpnLinkSrv { get; } = vpn_link_srv;
    private Lazy<IJobContext> JobContext { get; } = job_context;
    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<ApiResult> ChangeVpnPassword(string target, string password)
    {
        var account = await AccountApp.Value.GetActiveUser(target);

        var plan = (await PlanStateRepo.Value.GetPlanState(account.Id)) ??
                   throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                       $"There is not ant plan for user: {target}");

        var info = await ServerMngSrv.Value.GetDataPlaneInfo(plan.RestrictedRealmId);

        if (info?.SupportsCredentialRotation == true)
        {
            return await RotateConnectionId(account, plan);
        }

        return await ChangeRadiusPassword(account, plan, info, password);
    }

    public async Task<ApiResult> SendCertEmail(string target)
    {
        var (account, realm_id, info) = await LoadConfigContext(target, require_email: true);

        if (info?.SupportsCredentialRotation == true)
        {
            var link = await FetchVlessLink(realm_id, account.Username);

            await EmailSrv.Value.SendVpnLinkEmail(account.Fullname, account.EmailAddress!, new VpnLinkEmailContext
            {
                Username = account.Username,
                Link = link,
            });

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.Id,
                Category = EventCategory.Security,
                Type = EventType.Information,
                Title = "امنیت",
                Description = "کانفیگ اتصال به ایمیل ارسال شد.",
            });

            return ApiResult.Success("کانفیگ اتصال به ایمیل ارسال شد.");
        }

        var cert_context = await BuildCertificate(account, realm_id, info);

        var email_context = new CertEmailContext
        {
            Username = account.Username,
            Password = account.VpnPassword,
            Realm = cert_context.Realm,
            PrivateKeyOvpn = cert_context.PrivateKeyOvpn,
            CertFile = cert_context.CertFile,
        };

        await EmailSrv.Value.SendCertEmail(account.Fullname, account.EmailAddress!, email_context);

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.Id,
            Category = EventCategory.Security,
            Type = EventType.Information,
            Title = "امنیت",
            Description = "ایمیل گواهی اتصال ارسال شد.",
        });

        return ApiResult.Success("ایمیل گواهی اتصال ارسال شد.");
    }

    public async Task<CertContext> DownloadCertificate(string target)
    {
        var (account, cert_context) = await BuildCertificate(target, require_email: false);

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.Id,
            Category = EventCategory.Security,
            Type = EventType.Information,
            Title = "امنیت",
            Description = "کانفیگ اتصال دانلود شد.",
        });

        return cert_context;
    }

    public async Task<ApiResult<VpnConfigInfoModel>> GetConfigInfo(string target)
    {
        var (account, realm_id, info) = await LoadConfigContext(target, require_email: false);

        if (info?.SupportsCredentialRotation != true)
        {
            return ApiResult<VpnConfigInfoModel>.Success(new VpnConfigInfoModel { Kind = info?.Kind });
        }

        var link = await FetchVlessLink(realm_id, account.Username);

        return ApiResult<VpnConfigInfoModel>.Success(new VpnConfigInfoModel
        {
            Kind = info.Kind,
            VlessLink = link,
        });
    }

    public async Task<VpnConfigFileModel> DownloadVpnConfig(string target)
    {
        var (account, realm_id, info) = await LoadConfigContext(target, require_email: false);

        if (info?.SupportsCredentialRotation == true)
        {
            var link = await FetchVlessLink(realm_id, account.Username);

            var content = Encoding.UTF8.GetBytes($"{account.Username}\n{link}\n");

            await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
            {
                Target = account.Id,
                Category = EventCategory.Security,
                Type = EventType.Information,
                Title = "امنیت",
                Description = "کانفیگ اتصال دانلود شد.",
            });

            return new VpnConfigFileModel
            {
                FileName = $"{account.Username}.txt",
                ContentType = "text/plain",
                Content = content,
            };
        }

        var cert_context = await BuildCertificate(account, realm_id, info);

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.Id,
            Category = EventCategory.Security,
            Type = EventType.Information,
            Title = "امنیت",
            Description = "کانفیگ اتصال دانلود شد.",
        });

        return new VpnConfigFileModel
        {
            FileName = $"{account.Username}.ovpn",
            ContentType = "application/x-openvpn-profile",
            Content = cert_context.CertFile,
        };
    }

    public async Task<ApiResult<TrafficDataModel>> TrafficData(string target)
    {
        var account_id = await AccountRepo.Value.GetActiveAccountId(target);
        if (!account_id.HasValue)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var min_date_time = DateTime.Now.AddDays(-MaxDateBefore);
        var list = await TrafficDataRepo.Value.Fetch(account_id.Value, min_date_time);

        var result = ConvertToModel(list);

        return ApiResult<TrafficDataModel>.Success(result);
    }

    private async Task<ApiResult> RotateConnectionId(AccountEntity account, PlanStateEntity plan)
    {
        CheckRotationThrottle(account);

        // The dispatcher generates one UUID per request and applies it to every
        // x-ui server of the realm; the returned value is the effective
        // credential and the only thing allowed into VpnPassword (docs/16 D4).
        var uuid = await AccountRadiusSrv.Value.ChangeVpnPassword(plan.RestrictedRealmId, account.Username, string.Empty);

        account.VpnPassword = uuid;
        account.LastCredentialRotateTime = DateTime.Now;
        await AccountRepo.Value.Save(account);

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.Id,
            Category = EventCategory.Security,
            Type = EventType.Success,
            Title = "امنیت",
            Description = "چرخش شناسه اتصال انجام شد.",
        });

        return ApiResult.Success("چرخش شناسه اتصال انجام شد؛ کانفیگ قبلی باطل شد. لطفاً کانفیگ جدید خود را دریافت کنید.");
    }

    private void CheckRotationThrottle(AccountEntity account)
    {
        var window_minutes = Options.Value.MinUuidRotationMinutes;

        if (window_minutes <= 0 || account.LastCredentialRotateTime is not { } last_rotate)
        {
            return;
        }

        var elapsed_minutes = (DateTime.Now - last_rotate).TotalMinutes;

        if (elapsed_minutes >= window_minutes)
        {
            return;
        }

        var remaining = Math.Max(1, (int)Math.Ceiling(window_minutes - elapsed_minutes));

        throw new UserException($"چرخش شناسه اتصال تا {remaining} دقیقه‌ی دیگر ممکن نیست.",
            $"UUID rotation throttled: remaining-minutes={remaining}, window-minutes={window_minutes}");
    }

    private async Task<ApiResult> ChangeRadiusPassword(AccountEntity account, PlanStateEntity plan, DataPlaneInfoModel? info,
        string password)
    {
        if (info?.SupportsPasswordChange != true)
        {
            throw new UserException("تغییر کلمه عبور VPN برای این سرور ممکن نیست؛ در صورت نیاز با پشتیبانی تماس بگیرید.",
                $"VPN password change is not supported by the data-plane: realm={plan.RestrictedRealmId}");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new UserException("کلمه عبور خالی است!",
                "A password is required for Radius data-planes; only x-ui rotation may omit it.");
        }

        var effective_password = await AccountRadiusSrv.Value.ChangeVpnPassword(plan.RestrictedRealmId, account.Username, password);

        account.VpnPassword = effective_password;
        await AccountRepo.Value.Save(account);

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.Id,
            Category = EventCategory.Security,
            Type = EventType.Success,
            Title = "امنیت",
            Description = "تغییر کلمه عبور VPN.",
        });

        return ApiResult.Success("کلمه عبور VPN تغییر کرد.");
    }

    private async Task<(AccountEntity Account, int? RealmId, DataPlaneInfoModel? Info)> LoadConfigContext(string target,
        bool require_email)
    {
        var account = await AccountApp.Value.GetActiveUser(target);

        if (require_email && account.EmailAddress == null)
        {
            throw new UserException("ایمیل کاربر ثبت نشده است!",
                $"account is email address is unknown: target={target}");
        }

        var plan = (await PlanStateRepo.Value.GetPlanState(account.Id)) ??
                   throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                       $"There is not ant plan for user {target}");

        var info = await ServerMngSrv.Value.GetDataPlaneInfo(plan.RestrictedRealmId);

        return (account, plan.RestrictedRealmId, info);
    }

    private async Task<string> FetchVlessLink(int? realm_id, string username)
    {
        try
        {
            return await VpnLinkSrv.Value.GetVlessLink(realm_id, username);
        }
        catch (Exception error)
        {
            throw new UserException("دریافت کانفیگ اتصال از سرور در حال حاضر ممکن نیست؛ لطفاً بعداً تلاش کنید.",
                $"The vless link could not be fetched: realm={realm_id}, user={username}: {error.Message}");
        }
    }

    private async Task<CertContext> BuildCertificate(AccountEntity account, int? realm_id, DataPlaneInfoModel? info)
    {
        var usage = await PlanUsageReader.Value.GetUsage(account.Id);
        var time_left = usage?.ExpirationDate - DateTime.Now;

        if (time_left is { TotalMinutes: < 1 } ||
            usage?.TrafficLeft is < 1)
        {
            throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                $"There is not ant plan for user {account.Username}");
        }

        if (info?.SupportsOvpnCert != true)
        {
            throw new UserException("کانفیگ اتصال این سرور از نوع OpenVPN نیست؛ از لینک اتصال یا روش اتصال دیگر استفاده کنید.",
                $"OVPN certificate is not available for the data-plane: realm={realm_id}");
        }

        var cert_context = await ServerMngSrv.Value.GetDefaultCertificate(realm_id);

        await AccountRadiusSrv.Value.GetOVpnCertificate(realm_id, account.Username, cert_context);

        return cert_context;
    }

    private async Task<(AccountEntity Account, CertContext Cert)> BuildCertificate(string target, bool require_email)
    {
        var account = await AccountApp.Value.GetActiveUser(target);

        if (require_email && account.EmailAddress == null)
        {
            throw new UserException("ایمیل کاربر ثبت نشده است!",
                $"account is email address is unknown: target={target}");
        }

        var plan = (await PlanStateRepo.Value.GetPlanState(account.Id)) ??
                   throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                       $"There is not ant plan for user {target}");

        var info = await ServerMngSrv.Value.GetDataPlaneInfo(plan.RestrictedRealmId);

        var cert_context = await BuildCertificate(account, plan.RestrictedRealmId, info);

        return (account, cert_context);
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

            var data_in = (int)(record.Sum(x => x.DataIn) / StaticValues.BytesInMegDouble);
            var data_out = (int)(record.Sum(x => x.DataOut) / StaticValues.BytesInMegDouble);

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
                    Title = "مجموع",
                    Data = [.. total],
                }
            ],
            Labels = labels,
        };
    }
}
