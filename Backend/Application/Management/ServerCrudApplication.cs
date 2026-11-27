using PhotonBypass.Application.Account;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using PhotonBypass.Tools;
using Serilog;
using System.Net;
using System.Net.Sockets;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Application.Management;

/// <summary>
/// Admin server CRUD (Docs/20 phase 5, D8). The list returns scalar fields
/// plus a has-config flag — the stored (protected) config never leaves the
/// server (write-without-read). Boundary of Docs/16: nothing here touches the
/// keyed dispatch, adapters or <see cref="ServerFeature"/> itself.
/// </summary>
public interface IServerCrudApplication
{
    Task<ApiResult<List<ServerRowModel>>> GetList();

    Task<ApiResult> Create(SaveServerModel model);

    Task<ApiResult> Update(SaveServerModel model);

    Task<ApiResult> SetActive(int id, bool active);
}

class ServerCrudApplication(
    IServerRepository server_repo,
    Lazy<IRealmRepository> realm_repo,
    Lazy<ISelfServiceGuard> self_service_guard) : IServerCrudApplication
{
    // mirror the Server table column sizes (Database/LocalDatabase/Server.sql)
    private const int NameMaxLength = 15;

    private const int DomainMaxLength = 255;

    private const int ConfigMaxLength = 4000;

    /// <summary>
    /// Upper bound of <see cref="ServerEntity.CapacityBytes"/> (1 PiB): the admin form takes
    /// gigabytes, so a unit mistake (bytes typed as GB) must not pass (Docs/24 D4).
    /// </summary>
    private const long MaxServerCapacityBytes = 1L << 50;

    /// <summary>The service address every tunnel-mode server row carries (Docs/25 §4.1).</summary>
    private const string LoopbackAddress = "127.0.0.1";

    private IServerRepository ServerRepo { get; } = server_repo;

    private Lazy<IRealmRepository> RealmRepo { get; } = realm_repo;

    private Lazy<ISelfServiceGuard> SelfServiceGuard { get; } = self_service_guard;

    public async Task<ApiResult<List<ServerRowModel>>> GetList()
    {
        await RequireAdmin();

        var servers = await ServerRepo.GetAllServers();

        var realms = await RealmRepo.Value.GetByIds([.. servers.Select(s => s.RealmId).Distinct()]);

        var items = servers.Select(server => new ServerRowModel
        {
            Id = server.Id,
            IsActive = server.IsActive,
            RealmId = server.RealmId,
            Realm = realms.TryGetValue(server.RealmId, out var realm) ? realm.Name : null,
            IpAddress = server.IpAddress,
            Name = server.Name,
            DomainName = server.DomainName,
            CapacityBytes = server.CapacityBytes,
            MaxClients = server.MaxClients,
            OsType = server.OsType,
            Features = server.Features,
            HasConfig = server.JsonConfig != null,
            Created = server.Created,
            CreatedTitle = server.Created.ToPersianString(),
        });

        return ApiResult<List<ServerRowModel>>.Success([.. items]);
    }

    public async Task<ApiResult> Create(SaveServerModel model)
    {
        await RequireAdmin();

        model = Normalize(model);

        await Validate(model, exclude_id: 0);

        var server = new ServerEntity
        {
            IsActive = true,
            RealmId = model.RealmId,
            IpAddress = model.IpAddress,
            Name = model.Name,
            DomainName = model.DomainName,
            CapacityBytes = model.CapacityBytes,
            MaxClients = model.MaxClients,
            OsType = model.OsType,
            Features = model.Features,
        };

        ApplyConfig(server, model.Config);

        await ServerRepo.Save(server);

        Log.Information("Server created by admin: id={0}, name={1}, realm={2}, features={3}",
            server.Id, server.Name, server.RealmId, server.Features);

        return ApiResult.Success("سرور ساخته شد.");
    }

    public async Task<ApiResult> Update(SaveServerModel model)
    {
        await RequireAdmin();

        if (model.Id < 1)
        {
            throw new UserException("شناسه سرور نامعتبر است!", $"Server update requires an id: {model.Id}");
        }

        model = Normalize(model);

        await Validate(model, exclude_id: model.Id);

        var server = await ServerRepo.GetServerById(model.Id)
                     ?? throw new UserException("سرور پیدا نشد!", $"Server not found. id:{model.Id}");

        server.RealmId = model.RealmId;
        server.IpAddress = model.IpAddress;
        server.Name = model.Name;
        server.DomainName = model.DomainName;
        server.CapacityBytes = model.CapacityBytes;
        server.MaxClients = model.MaxClients;
        server.OsType = model.OsType;
        server.Features = model.Features;

        // null config keeps the stored protected blob byte-for-byte — it is
        // never decrypted or returned (Docs/20 phase 5: write-without-read)
        ApplyConfig(server, model.Config);

        await ServerRepo.Save(server);

        Log.Information("Server updated by admin: id={0}, name={1}, realm={2}, features={3}",
            server.Id, server.Name, server.RealmId, server.Features);

        return ApiResult.Success("سرور به‌روزرسانی شد.");
    }

    public async Task<ApiResult> SetActive(int id, bool active)
    {
        await RequireAdmin();

        if (id < 1)
        {
            throw new UserException("شناسه سرور نامعتبر است!", $"Invalid server id: {id}");
        }

        var server = await ServerRepo.GetServerById(id)
                     ?? throw new UserException("سرور پیدا نشد!", $"Server not found. id:{id}");

        server.IsActive = active;

        await ServerRepo.Save(server);

        Log.Information("Server active flag changed by admin: id={0}, name={1}, active={2}",
            server.Id, server.Name, active);

        return ApiResult.Success(active ? "سرور فعال شد." : "سرور غیرفعال شد.");
    }

    private async Task RequireAdmin()
    {
        // defense in depth behind the Admin policy (same as Docs/20 phase 4)
        var actor = await SelfServiceGuard.Value.ResolveActor();

        if (actor.Role != AccountRole.Admin)
        {
            throw new UserException("مدیریت سرورها فقط برای ادمین مجاز است!",
                $"Server CRUD is admin-only (Docs/20 §3): actor={actor.Username}, role={actor.Role}", 403);
        }
    }

    private static SaveServerModel Normalize(SaveServerModel model)
    {
        model.IpAddress = model.IpAddress?.Trim() ?? string.Empty;
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.DomainName = model.DomainName?.Trim() ?? string.Empty;

        if (model.Config?.SshConfig is { } ssh)
        {
            // the stored pin is canonical whatever shape it was pasted in (Docs/25 §4.6)
            var pin = SshTunnelConfig.NormalizeHostKeyPin(ssh.HostKeySha256);

            ssh.HostKeySha256 = pin.Length < 1 ? null : pin;
            ssh.Username = ssh.Username?.Trim() ?? string.Empty;
            ssh.PrivateKey = string.IsNullOrWhiteSpace(ssh.PrivateKey) ? null : ssh.PrivateKey.Trim();
        }

        return model;
    }

    /// <summary>
    /// Channel rules of the server config (Docs/25 §4.6). The direct transport
    /// still accepts a row without key and pin; the tunnel transport refuses
    /// anything it cannot authenticate or pin with.
    /// </summary>
    private static void ValidateConfiguration(SaveServerModel model)
    {
        var config = model.Config!;

        var tunnel = config.Transport == ServerTransport.SshTunnel;

        var ssh = config.SshConfig;

        if (ssh != null && ssh.Port is < 1 or > 65535)
        {
            throw new UserException("پورت SSH نامعتبر است!", $"SshConfig.Port must be in 1..65535: {ssh.Port}");
        }

        if (tunnel)
        {
            if (ssh == null || ssh.Username.Length < 1)
            {
                throw new UserException("برای حالت تانل، کاربر SSH الزامی است!",
                    $"SshTunnel requires SshConfig with a Username: server '{model.Name}'");
            }

            if (!SshTunnelConfig.HasOpenSshKey(ssh.PrivateKey))
            {
                throw new UserException("کلید خصوصی SSH معتبر نیست!",
                    $"SshTunnel requires an OpenSSH private key ({SshTunnelConfig.OpenSshKeyHeader}): server '{model.Name}'");
            }

            if (!SshTunnelConfig.IsValidHostKeyPin(ssh.HostKeySha256))
            {
                throw new UserException("کلید میزبان (host key) ثبت نشده یا نامعتبر است!",
                    $"SshTunnel requires HostKeySha256 as SHA256:<43 base64 characters>; run the connection test to discover it: server '{model.Name}'");
            }
        }

        var web = config.WebApiConfig;

        var xui = (model.Features & ServerFeature.Xui) != 0;

        var user_manager = (model.Features & ServerFeature.UserManager) != 0;

        if (xui && web == null)
        {
            throw new UserException("برای سرور x-ui پیکربندی وب لازم است!",
                $"Servers with the Xui feature need WebApiConfig: server '{model.Name}'");
        }

        if (tunnel)
        {
            if (xui)
            {
                if (web!.HostName != LoopbackAddress || web.Port == null || web.Ssl)
                {
                    throw new UserException("در حالت تانل، آدرس پنل x-ui باید `127.0.0.1` با پورت مشخص و بدون SSL باشد!",
                        $"SshTunnel needs WebApiConfig.HostName=127.0.0.1, a Port and Ssl=false for x-ui servers: server '{model.Name}'");
                }
            }
            // only a server with a service to reach needs the service address; a NAS that is only
            // reached over SSH (scripts, OpenVPN files) carries no web configuration
            else if (user_manager && (web == null || web.HostName != LoopbackAddress || web.Port == null))
            {
                throw new UserException("در حالت تانل، آدرس سرویس باید `127.0.0.1` با پورت مشخص باشد!",
                    $"SshTunnel needs WebApiConfig.HostName=127.0.0.1 and a Port: server '{model.Name}'");
            }
        }

        // the panel login needs both; a row without them could only be saved to fail at its first request
        if (xui && (string.IsNullOrWhiteSpace(web!.Username) || string.IsNullOrWhiteSpace(web.Password)))
        {
            throw new UserException("برای سرور x-ui اعتبار پنل لازم است!",
                $"Servers with the Xui feature need WebApiConfig.Username and Password: server '{model.Name}'");
        }

        if (user_manager &&
            (web == null || string.IsNullOrWhiteSpace(web.Username) || string.IsNullOrWhiteSpace(web.Password)))
        {
            throw new UserException("برای User Manager اعتبار API لازم است!",
                $"Servers with the UserManager feature need WebApiConfig.Username and Password: server '{model.Name}'");
        }
    }

    private async Task Validate(SaveServerModel model, int exclude_id)
    {
        if (!IsValidIPv4(model.IpAddress))
        {
            throw new UserException("آدرس IP سرور نامعتبر است!",
                $"Server IP must be a valid IPv4 (column is varchar(15)): {model.IpAddress}");
        }

        if (model.Name.Length < 1 || model.Name.Length > NameMaxLength)
        {
            throw new UserException("نام سرور نامعتبر است!",
                $"Server name length must be 1..{NameMaxLength}: '{model.Name}'");
        }

        if (model.DomainName.Length < 1 || model.DomainName.Length > DomainMaxLength)
        {
            throw new UserException("دامنه سرور نامعتبر است!",
                $"Server domain length must be 1..{DomainMaxLength}: '{model.DomainName}'");
        }

        if (model.CapacityBytes < 1)
        {
            throw new UserException("ظرفیت ترافیک سرور نامعتبر است!",
                $"Server capacity must be positive (bytes): {model.CapacityBytes}");
        }

        if (model.CapacityBytes > MaxServerCapacityBytes)
        {
            throw new UserException("ظرفیت ترافیک سرور بیش از حد بزرگ است!",
                $"Server capacity must not exceed {MaxServerCapacityBytes} bytes (1 PiB): {model.CapacityBytes}");
        }

        if (model.MaxClients is < 1)
        {
            throw new UserException("سقف تعداد کاربر سرور نامعتبر است!",
                $"Server max-clients must be null or >= 1: {model.MaxClients}");
        }

        if (!Enum.IsDefined(model.OsType))
        {
            throw new UserException("سیستم‌عامل سرور نامعتبر است!", $"Unknown server OS type: {model.OsType}");
        }

        if (model.Features == 0 || (model.Features & ~KnownFeatures) != 0)
        {
            throw new UserException("قابلیت‌های انتخاب‌شده برای سرور نامعتبر است!",
                $"Server features must be non-zero known ServerFeature bits: {model.Features}");
        }

        if (model.Config != null) ValidateConfiguration(model);

        var realms = await RealmRepo.Value.GetByIds([model.RealmId]);

        if (!realms.ContainsKey(model.RealmId))
        {
            throw new UserException("قلم (realm) سرور نامعتبر است!", $"Unknown realm id: {model.RealmId}");
        }

        await EnsureNoConflicts(model, exclude_id);
    }

    private async Task EnsureNoConflicts(SaveServerModel model, int exclude_id)
    {
        // duplicates would break the singleton lookups (GetServerIdByIpAddress)
        // and silently collapse per-realm NAS selection — same 409 contract as
        // the account unique fields (Docs/20 phase 4)
        var conflicts = await ServerRepo.FindConflicts(model.IpAddress, model.Name, model.DomainName, exclude_id);

        if (conflicts.Count < 1) return;

        if (conflicts.Any(s => s.IpAddress.Equals(model.IpAddress, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UserException("این آدرس IP روی سرور دیگری ثبت شده است!",
                $"Duplicate server IP: {model.IpAddress}", 409);
        }

        if (conflicts.Any(s => s.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UserException("این نام سرور قبلا استفاده شده است!",
                $"Duplicate server name: {model.Name}", 409);
        }

        throw new UserException("این دامنه روی سرور دیگری ثبت شده است!",
            $"Duplicate server domain: {model.DomainName}", 409);
    }

    private static void ApplyConfig(ServerEntity server, ServerConfiguration? config)
    {
        if (config == null) return;

        // re-protect from the sent JSON; the old blob is simply replaced
        server.Config = config;

        if (server.JsonConfig!.Length > ConfigMaxLength)
        {
            throw new UserException("حجم کانفیگ ارسالی بیش از حد مجاز است!",
                $"Protected server config exceeds varchar({ConfigMaxLength}): {server.JsonConfig.Length}");
        }
    }

    private static bool IsValidIPv4(string ip)
    {
        if (ip.Length > 15) return false;

        return IPAddress.TryParse(ip, out var parsed) &&
               parsed.AddressFamily == AddressFamily.InterNetwork;
    }

    private static readonly ServerFeature KnownFeatures =
        Enum.GetValues<ServerFeature>().Aggregate((current, feature) => current | feature);
}
