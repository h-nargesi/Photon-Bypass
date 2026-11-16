using Microsoft.Extensions.Options;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Vpn.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Management.Model;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using System.IO.Compression;
using System.Text;

namespace PhotonBypass.Application.Vpn;

class VpnApplication(
    Lazy<IPanelUserRepository> panel_user_repo,
    Lazy<IVpnAccountRepository> vpn_account_repo,
    Lazy<IRealmRepository> realm_repo,
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
    IOptions<ManagementOptions> options,
    IOptions<BrandingOptions> branding_options)
    : IVpnApplication
{
    private const int MaxDateBefore = 30;

    private static readonly string HeaderRule = new('=', 36);

    private Lazy<IPanelUserRepository> PanelUserRepo { get; } = panel_user_repo;
    private Lazy<IVpnAccountRepository> VpnAccountRepo { get; } = vpn_account_repo;
    private Lazy<IRealmRepository> RealmRepo { get; } = realm_repo;
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
    private IOptions<BrandingOptions> BrandingOptions { get; } = branding_options;

    public async Task<ApiResult<List<VpnAccountSummaryModel>>> GetAccounts(string? target)
    {
        var owner = await AccountApp.Value.GetActiveUser(target ?? JobContext.Value.Target);

        var accounts = await VpnAccountRepo.Value.GetVisibleByUser(owner.Id);
        var realm_names = await LoadRealmNames(accounts.Select(account => account.RealmId));

        var result = accounts.Select(account => new VpnAccountSummaryModel
            {
                Id = account.Id,
                Username = account.Username,
                RealmId = account.RealmId,
                RealmName = realm_names.GetValueOrDefault(account.RealmId),
                Status = (int)account.Status,
                Created = account.Created,
            })
            .ToList();

        return ApiResult<List<VpnAccountSummaryModel>>.Success(result);
    }

    public async Task<ApiResult> ChangeVpnPassword(int account_id, string password)
    {
        var account = await GetActiveAccount(account_id);

        await EnsureActivePlan(account);

        var info = await ServerMngSrv.Value.GetDataPlaneInfo(account.RealmId);

        if (info?.SupportsCredentialRotation == true)
        {
            return await RotateConnectionId(account);
        }

        return await ChangeRadiusPassword(account, info, password);
    }

    public async Task<ApiResult> SendCertEmail(int account_id)
    {
        var (account, owner, realm_id, info) = await LoadConfigContext(account_id, require_email: true);

        var has_link = info?.SupportsCredentialRotation == true;
        var has_cert = info?.SupportsOvpnCert == true;

        if (!has_link && !has_cert)
        {
            throw new UserException("کانفیگ اتصال این سرور از نوع OpenVPN نیست؛ از لینک اتصال یا روش اتصال دیگر استفاده کنید.",
                $"Neither a vless link nor an OVPN certificate is available for the data-plane: realm={realm_id}");
        }

        // One request builds every connection artifact the account owns and
        // delivers them in a single email; template sections without a value
        // are stripped by the email service.
        var email_context = new ConnectionEmailContext
        {
            Username = account.Username,
        };

        if (has_link)
        {
            email_context.Link = await FetchVlessLink(realm_id, account.Username);
        }

        if (has_cert)
        {
            var cert_context = await BuildCertificate(account, realm_id, info);

            email_context.Password = account.VpnPassword;
            email_context.PrivateKeyOvpn = cert_context.PrivateKeyOvpn;
            email_context.CertFile = cert_context.CertFile;
        }

        await EmailSrv.Value.SendConnectionEmail(owner.Fullname, owner.EmailAddress!, email_context);

        await SaveAccountHistory(account, "کانفیگ اتصال به ایمیل ارسال شد.");

        return ApiResult.Success("کانفیگ اتصال به ایمیل ارسال شد.");
    }

    public async Task<ApiResult<VpnConfigInfoModel>> GetConfigInfo(int account_id)
    {
        var (account, _, realm_id, info) = await LoadConfigContext(account_id, require_email: false);

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

    /// <summary>
    /// Single config download for every data-plane kind: x-ui accounts get a
    /// plain-text connection sheet ({username}.txt), OpenVPN accounts get a ZIP
    /// ({username}.zip) carrying the certificate plus the same info sheet.
    /// </summary>
    public async Task<VpnConfigFileModel> DownloadVpnConfig(int account_id)
    {
        var (account, _, realm_id, info) = await LoadConfigContext(account_id, require_email: false);

        VpnConfigFileModel file;

        if (info?.SupportsCredentialRotation == true)
        {
            var link = await FetchVlessLink(realm_id, account.Username);

            file = new VpnConfigFileModel
            {
                FileName = $"{account.Username}.txt",
                ContentType = "text/plain; charset=utf-8",
                Content = Encoding.UTF8.GetBytes(BuildConnectionInfoText(account.Username, link: link, cert_key_password: null)),
            };
        }
        else
        {
            var cert_context = await BuildCertificate(account, realm_id, info);

            file = new VpnConfigFileModel
            {
                FileName = $"{account.Username}.zip",
                ContentType = "application/zip",
                Content = BuildConfigZip(account.Username, cert_context),
            };
        }

        await SaveAccountHistory(account, "کانفیگ اتصال دانلود شد.");

        return file;
    }

    /// <summary>
    /// Builds the shared connection sheet: Persian label on one line, the
    /// (mostly LTR) value on the next, exactly one blank line between
    /// sections; full Persian sentences stay unsplit.
    /// </summary>
    private string BuildConnectionInfoText(string username, string? link, string? cert_key_password)
    {
        var branding = BrandingOptions.Value;

        var sections = new List<string>
        {
            $"{HeaderRule}\n{branding.Title} | مشخصات اتصال\n{HeaderRule}",
            $"نام کاربری:\n{username}",
        };

        if (cert_key_password != null)
        {
            sections.Add($"رمز گواهی (User key password):\n{cert_key_password}");
        }

        if (link != null)
        {
            sections.Add($"لینک اتصال (vless):\n{link}");
        }

        sections.Add($"راهنمای اتصال:\n{branding.ConnectionGuideUrl}");

        if (!string.IsNullOrWhiteSpace(branding.OvpnInstallerUrl))
        {
            sections.Add($"نصب‌کننده OpenVPN:\n{branding.OvpnInstallerUrl}");
        }

        sections.Add($"سایت:\n{branding.SiteUrl}");

        if (link != null)
        {
            sections.Add("نکته:\nبا هر چرخش شناسه اتصال، کانفیگ قبلی باطل می‌شود؛ کانفیگ جدید را از پنل کاربری دریافت کنید.");
        }

        return string.Join("\n\n", sections) + "\n";
    }

    private byte[] BuildConfigZip(string username, CertContext cert_context)
    {
        using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
        {
            var cert_entry = archive.CreateEntry("config.ovpn");

            using (var cert_stream = cert_entry.Open())
            {
                cert_stream.Write(cert_context.CertFile, 0, cert_context.CertFile.Length);
            }

            var info_entry = archive.CreateEntry("connection-info.txt");

            using (var info_stream = info_entry.Open())
            {
                var info = Encoding.UTF8.GetBytes(
                    BuildConnectionInfoText(username, link: null, cert_key_password: cert_context.PrivateKeyOvpn));

                info_stream.Write(info, 0, info.Length);
            }
        }

        return output.ToArray();
    }

    public async Task<ApiResult<TrafficDataModel>> TrafficData(int account_id)
    {
        var account = await GetActiveAccount(account_id);

        var min_date_time = DateTime.Now.AddDays(-MaxDateBefore);
        var list = await TrafficDataRepo.Value.Fetch(account.Id, min_date_time);

        var result = ConvertToModel(list);

        return ApiResult<TrafficDataModel>.Success(result);
    }

    private async Task<ApiResult> RotateConnectionId(VpnAccountEntity account)
    {
        CheckRotationThrottle(account);

        // The dispatcher generates one UUID per request and applies it to every
        // x-ui server of the realm; the returned value is the effective
        // credential and the only thing allowed into VpnPassword (docs/16 D4).
        var uuid = await AccountRadiusSrv.Value.ChangeVpnPassword(account.RealmId, account.Username, string.Empty);

        account.VpnPassword = uuid;
        account.LastCredentialRotateTime = DateTime.Now;
        await VpnAccountRepo.Value.Save(account);

        await SaveAccountHistory(account, "چرخش شناسه اتصال انجام شد.");

        return ApiResult.Success("چرخش شناسه اتصال انجام شد؛ کانفیگ قبلی باطل شد. لطفاً کانفیگ جدید خود را دریافت کنید.");
    }

    private void CheckRotationThrottle(VpnAccountEntity account)
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

    private async Task<ApiResult> ChangeRadiusPassword(VpnAccountEntity account, DataPlaneInfoModel? info, string password)
    {
        if (info?.SupportsPasswordChange != true)
        {
            throw new UserException("تغییر کلمه عبور VPN برای این سرور ممکن نیست؛ در صورت نیاز با پشتیبانی تماس بگیرید.",
                $"VPN password change is not supported by the data-plane: realm={account.RealmId}");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new UserException("کلمه عبور خالی است!",
                "A password is required for Radius data-planes; only x-ui rotation may omit it.");
        }

        var effective_password = await AccountRadiusSrv.Value.ChangeVpnPassword(account.RealmId, account.Username, password);

        account.VpnPassword = effective_password;
        await VpnAccountRepo.Value.Save(account);

        await SaveAccountHistory(account, "تغییر کلمه عبور VPN.");

        return ApiResult.Success("کلمه عبور VPN تغییر کرد.");
    }

    private async Task<(VpnAccountEntity Account, PanelUserEntity Owner, int RealmId, DataPlaneInfoModel? Info)> LoadConfigContext(
        int account_id, bool require_email)
    {
        var account = await GetActiveAccount(account_id);
        var owner = await PanelUserRepo.Value.GetActiveUser(account.UserId)
                    ?? throw new UserException("کاربر پیدا نشد!", $"Owner not found or inactive: id={account.UserId}");

        if (require_email && owner.EmailAddress == null)
        {
            throw new UserException("ایمیل کاربر ثبت نشده است!",
                $"account is email address is unknown: target={owner.Username}");
        }

        var plan = (await PlanStateRepo.Value.GetPlanState(account.Id)) ??
                   throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                       $"There is not ant plan for user {account.Username}");

        var info = await ServerMngSrv.Value.GetDataPlaneInfo(account.RealmId);

        return (account, owner, account.RealmId, info);
    }

    /// <summary>Credential/connection operations require an active plan (docs/08 contract).</summary>
    private async Task EnsureActivePlan(VpnAccountEntity account)
    {
        var plan = await PlanStateRepo.Value.GetPlanState(account.Id);

        if (plan == null)
        {
            throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                $"There is not ant plan for user {account.Username}");
        }
    }

    private async Task<VpnAccountEntity> GetActiveAccount(int account_id)
    {
        var account = await VpnAccountRepo.Value.GetVisibleAccount(account_id)
                      ?? throw new UserException("اکانت VPN پیدا نشد یا غیرفعال است!",
                          $"VPN account not found or deactivated: id={account_id}");

        if (account.Status == VpnAccountStatus.Suspended)
        {
            throw new UserException("سرویس VPN این اکانت توسط مدیریت غیرفعال است!",
                $"VPN account is suspended: id={account_id}, user={account.Username}");
        }

        return account;
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

    private async Task<CertContext> BuildCertificate(VpnAccountEntity account, int? realm_id, DataPlaneInfoModel? info)
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

    private Task SaveAccountHistory(VpnAccountEntity account, string description)
    {
        return HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.UserId,
            VpnAccountId = account.Id,
            Category = EventCategory.Security,
            Type = EventType.Information,
            Title = "امنیت",
            Description = description,
        });
    }

    private async Task<Dictionary<int, string>> LoadRealmNames(IEnumerable<int> realm_ids)
    {
        var ids = realm_ids.Distinct().ToList();

        if (ids.Count == 0) return [];

        var realms = await RealmRepo.Value.GetByIds(ids);

        return realms.ToDictionary(pair => pair.Key, pair => pair.Value.Name);
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
