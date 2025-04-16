using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Application.Database;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra;
using PhotonBypass.Infra.Controller;
using PhotonBypass.OutSource;
using PhotonBypass.OutSource.Mikrotik.Model;

namespace PhotonBypass.Application.Connection;

class ConnectionApplication(
    IMikrotikHandler MikrotikHdl,
    NasRepository NasRepo,
    Lazy<RadAcctRepository> RadAcctRepo)
    : IConnectionApplication
{
    public async Task<ApiResult<IList<ConnectionStateModel>>> GetCurrentConnectionState(string target)
    {
        var current_con_list = await RadAcctRepo.Value.GetCurrentConnectionList(target);

        var cuurent_con_dict = current_con_list.GroupBy(x => x.NasIPAddress)
            .ToDictionary(k => k.Key, v => v.ToList());

        var servers_info = await NasRepo.GetNasInfo(cuurent_con_dict.Keys);

        var servers_task = new List<Task<(NasEntity server, IList<UserConnectionBinding> connections)>>();
        foreach (var server in servers_info)
            servers_task.Add(MikrotikHdl.GetActiveConnections(server.Value, target));

        var result = new List<ConnectionStateModel>();
        while (servers_task.Count > 0)
        {
            var server_result_task = await Task.WhenAny(servers_task);
            servers_task.Remove(server_result_task);
            var (server, server_connections) = await server_result_task;

            var connection_list = cuurent_con_dict[server.IpAddress];
            var active_connections = server_connections.Select(k => k.SessionId).ToHashSet();

            result.AddRange(cuurent_con_dict[server.IpAddress].Select(c => new ConnectionStateModel
            {
                Duration = (int)c.SessionUpTime.TotalMinutes,
                State = active_connections.Contains(c.AcctSessionId) ? ConnectionState.Up : ConnectionState.Down,
                Server = server.IpAddress,
                SessionId = c.AcctSessionId,
            }));
        }

        return ApiResult<IList<ConnectionStateModel>>.Success(result);
    }

    public async Task<ApiResult> CloseConnection(string server, string target, string sessionId)
    {
        if (!await NasRepo.Exists(server))
        {
            throw new UserException("دسترسی غیرمجاز!");
        }

        var result = await MikrotikHdl.CloseConnection(server, target, sessionId);

        if (!result)
        {
            throw new Exception("بستن کانکشن با خطا مواجه شد!");
        }

        return ApiResult.Success("کانکشن بسته شد.");
    }
}
