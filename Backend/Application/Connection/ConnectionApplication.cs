using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Servers;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;

namespace PhotonBypass.Application.Connection;

class ConnectionApplication(
    ISessionRadiusSyncService SessionRadiusSrv,
    IServerRepository server_repo,
    Lazy<IHistoryRepository> HistoryRepo,
    Lazy<IPlanStateRepository> PlanRepo,
    Lazy<IJobContext> JobContext)
    : IConnectionApplication
{
    public async Task<ApiResult<List<ConnectionStateModel>>> GetCurrentConnectionState(string target)
    {
        var target_realm_id = await PlanRepo.Value.GetActiveAccountRealmId(target);

        var connections = await SessionRadiusSrv.GetActiveConnections(target_realm_id, target);

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
        var server = (await server_repo.GetActiveNasInfo(ip)) ??
                     throw new UserException("دسترسی غیرمجاز!",
                         $"Closing connection ip is invalid: ({ip}, {target}, {session_id})");

        var result = await SessionRadiusSrv.DirectlyCloseConnection(server, session_id);

        if (!result)
        {
            throw new Exception("بستن کانکشن با خطا مواجه شد!");
        }

        _ = HistoryRepo.Value.Save(new HistoryEntity
        {
            Issuer = JobContext.Value.Username,
            Target = target,
            EventTime = DateTime.Now,
            Title = "کانکشن",
            Description = "کانکشن بسته شد.",
        });

        Log.Information("[user: {0}] Connection Closed: ({1}, {2}, {3})",
            JobContext.Value.Username, ip, target, session_id);

        return ApiResult.Success("کانکشن بسته شد.");
    }
}