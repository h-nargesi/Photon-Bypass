using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Servers;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Application.Connection;

class ConnectionApplication(
    Lazy<IVpnAccountRepository> vpn_account_repo,
    Lazy<IPlanStateRepository> plan_state_repo,
    ISessionRadiusSyncService session_radius_srv,
    IServerRepository server_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IJobContext> job_context)
    : IConnectionApplication
{
    private Lazy<IVpnAccountRepository> VpnAccountRepo { get; } = vpn_account_repo;
    private Lazy<IPlanStateRepository> PlanStateRepo { get; } = plan_state_repo;
    private ISessionRadiusSyncService SessionRadiusSrv { get; } = session_radius_srv;
    private IServerRepository ServerRepo { get; } = server_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult<List<ConnectionStateModel>>> GetCurrentConnectionState(int account_id)
    {
        var account = await GetAccount(account_id);

        await EnsureActivePlan(account);

        var connections = await SessionRadiusSrv.GetActiveConnections(account.RealmId, account.Username);

        var result = connections.Select(c => new ConnectionStateModel
            {
                SessionId = c.SessionId,
                Duration = (int)c.UpTime.TotalMinutes,
                State = c.State,
                Server = c.NasIpAddress,
            })
            .ToList();

        return ApiResult<List<ConnectionStateModel>>.Success(result);
    }

    public async Task<ApiResult> CloseConnection(string ip, int account_id, string session_id)
    {
        var account = await GetAccount(account_id);

        var server = (await ServerRepo.GetActiveNasInfo(ip)) ??
                     throw new UserException("دسترسی غیرمجاز!",
                         $"Closing connection ip is invalid: ({ip}, {account.Username}, {session_id})");

        if (server.RealmId != account.RealmId)
        {
            throw new UserException("دسترسی غیرمجاز به سرور!",
                $"Closing connection ip is invalid: ({ip}, {account.Username}, {session_id}, user-realm-id={account.RealmId})");
        }

        var own_sessions = await SessionRadiusSrv.GetActiveConnections(account.RealmId, account.Username);

        if (own_sessions.All(session => session.SessionId != session_id))
        {
            throw new UserException("دسترسی غیرمجاز!",
                $"Closing connection session does not belong to target: ({ip}, {account.Username}, {session_id})");
        }

        try
        {
            await SessionRadiusSrv.CloseConnectionBySessionId(server, session_id);
        }
        catch (Exception ex)
        {
            throw new UserException("بستن کانکشن با خطا مواجه شد!", ex);
        }

        await HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account.UserId,
            VpnAccountId = account.Id,
            Category = EventCategory.Action,
            Type = EventType.Success,
            Title = "کانکشن",
            Description = "کانکشن بسته شد.",
        });

        Log.Information("[user: {0}] Connection Closed: ({1}, {2})",
            JobContext.Value.Username, account.Username, session_id);

        return ApiResult.Success("کانکشن بسته شد.");
    }

    /// <summary>Live-session listing requires an active plan (Docs/08 contract).</summary>
    private async Task EnsureActivePlan(VpnAccountEntity account)
    {
        var plan = await PlanStateRepo.Value.GetPlanState(account.Id);

        if (plan == null)
        {
            throw new UserException("در حال حاضر هیچ پلنی برای این کاربر فعال نیست!",
                $"There is not ant plan for user {account.Username}");
        }
    }

    private async Task<VpnAccountEntity> GetAccount(int account_id)
    {
        return await VpnAccountRepo.Value.GetVisibleAccount(account_id)
               ?? throw new UserException("اکانت VPN پیدا نشد یا غیرفعال است!",
                   $"VPN account not found or deactivated: id={account_id}");
    }
}
