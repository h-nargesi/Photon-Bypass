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
    ISessionRadiusSyncService RadiusSrv,
    INasRepository NasRepo,
    Lazy<IAccountRepository> AccountRepo,
    Lazy<IJobContext> JobContext,
    Lazy<IHistoryRepository> HistoryRepo)
    : IConnectionApplication
{
    public async Task<ApiResult<List<ConnectionStateModel>>> GetCurrentConnectionState(string target)
    {
        var target_realm_id = await AccountRepo.Value.GetActiveAccountRealmId(target);

        if (target_realm_id == null)
        {
            return ApiResult<List<ConnectionStateModel>>.Success([]);
        }

        var servers_info = await NasRepo.GetAllActiveInRealm(target_realm_id.Value);

        var servers_task = servers_info.ToDictionary(
            server => server,
            server => RadiusSrv.GetActiveConnections(server, target));

        await Task.WhenAll(servers_task.Values);

        var result = servers_task.SelectMany(task =>
            task.Value.Result.Select(c => new ConnectionStateModel
            {
                SessionId = c.SessionId,
                Duration = (int)c.UpTime.TotalMinutes,
                State = c.State,
                Server = task.Key.IpAddress,
            }))
            .ToList();

        return ApiResult<List<ConnectionStateModel>>.Success(result);
    }

    public async Task<ApiResult> CloseConnection(string server, string target, string session_id)
    {
        var nas = (await NasRepo.GetActiveNasInfo(server))
                  ?? throw new UserException("دسترسی غیرمجاز!",
                      $"Closing connection server is invalid: ({server}, {target}, {session_id})");

        var result = await RadiusSrv.CloseConnection(nas, session_id);

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
            JobContext.Value.Username, server, target, session_id);

        return ApiResult.Success("کانکشن بسته شد.");
    }
}