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
    IAccountRepository account_repo,
    ISessionRadiusSyncService session_radius_srv,
    IServerRepository server_repo,
    Lazy<IHistoryRepository> history_repo,
    Lazy<IPlanStateRepository> plan_repo,
    Lazy<IJobContext> job_context)
    : IConnectionApplication
{
    private IAccountRepository AccountRepo { get; } = account_repo;
    private ISessionRadiusSyncService SessionRadiusSrv { get; } = session_radius_srv;
    private IServerRepository ServerRepo { get; } = server_repo;
    private Lazy<IHistoryRepository> HistoryRepo { get; } = history_repo;
    private Lazy<IPlanStateRepository> PlanRepo { get; } = plan_repo;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task<ApiResult<List<ConnectionStateModel>>> GetCurrentConnectionState(string target)
    {
        var account_id = await AccountRepo.GetActiveAccountId(target);
        if (!account_id.HasValue)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var target_realm_id = await PlanRepo.Value.GetActiveAccountRealmId(account_id.Value);

        if (!target_realm_id.HasValue)
        {
            throw new UserException("کاربر هیچ پلن فعالی ندارد!", $"There is not any plan for target={target}");
        }

        var connections = await SessionRadiusSrv.GetActiveConnections(target_realm_id.Value.RealmId, target);

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

    public async Task<ApiResult> CloseConnection(string ip, string target, string session_id)
    {
        var account_id = await AccountRepo.GetActiveAccountId(target);
        if (!account_id.HasValue)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: target={target}");
        }

        var target_realm_id = await PlanRepo.Value.GetActiveAccountRealmId(account_id.Value);

        if (!target_realm_id.HasValue)
        {
            throw new UserException("کاربر هیچ پلن فعالی ندارد!", $"There is not any plan for target={target}");
        }

        var server = (await ServerRepo.GetActiveNasInfo(ip)) ??
                     throw new UserException("دسترسی غیرمجاز!",
                         $"Closing connection ip is invalid: ({ip}, {target}, {session_id})");

        if (target_realm_id.HasValue && server.RealmId != target_realm_id.Value.RealmId)
        {
            throw new UserException("دسترسی غیرمجاز به سرور!",
                $"Closing connection ip is invalid: ({ip}, {target}, {session_id}, user-realm-id={target_realm_id})");
        }

        try
        {
            await SessionRadiusSrv.CloseConnectionBySessionId(server, session_id);
        }
        catch (Exception ex)
        {
            throw new UserException("بستن کانکشن با خطا مواجه شد!", ex);
        }

        _ = HistoryRepo.Value.Save(JobContext.Value.Username, new HistoryEntity
        {
            Target = account_id.Value,
            Category = EventCategory.Action,
            Type = EventType.Success,
            Title = "کانکشن",
            Description = "کانکشن بسته شد.",
        });

        Log.Information("[user: {0}] Connection Closed: ({1}, {2})",
            JobContext.Value.Username, target, session_id);

        return ApiResult.Success("کانکشن بسته شد.");
    }
}