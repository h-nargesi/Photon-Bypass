using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Application.Database;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Controller;
using PhotonBypass.OutSource;
using PhotonBypass.OutSource.Mikrotik.Model;

namespace PhotonBypass.Application.Connection;

class ConnectionApplication(
    IMikrotikHandler MikrotikHdl,
    Lazy<RadAcctRepository> RadAcctRepo,
    Lazy<NasRepository> NasRepo)
    : IConnectionApplication
{
    public async Task<ApiResult<IList<ConnectionStateModel>>> GetCurrentConnectionState(string target)
    {
        var current_con_list = await RadAcctRepo.Value.GetCurrentConnectionList(target);

        var cuurent_con_dict = current_con_list.GroupBy(x => x.NasIPAddress)
            .ToDictionary(k => k.Key, v => v.ToList());

        var servers_info = await NasRepo.Value.GetNasInfo(cuurent_con_dict.Keys);

        var servers_task = new List<Task<(NasEntity server, IList<UserConnectionBinding> connections)>>();
        foreach (var server in servers_info)
            servers_task.Add(MikrotikHdl.GetActiveConnections(server.Value, target));

        var result = new List<ConnectionStateModel>();
        while (servers_task.Count > 0)
        {
            var server_result_task = await Task.WhenAny(servers_task);
            servers_task.Remove(server_result_task);
            var server_result = await server_result_task;

            var connection_list = cuurent_con_dict[server_result.server.IpAddress];
            var active_connections = server_result.connections.Select(k => k.SessionId).ToHashSet();

            result.AddRange(cuurent_con_dict[server_result.server.IpAddress].Select(c => new ConnectionStateModel
            {
                Duration = (int)c.SessionUpTime.TotalMinutes,
                State = active_connections.Contains(c.AcctSessionId) ? ConnectionState.Up : ConnectionState.Down,
            }));
        }

        return ApiResult<IList<ConnectionStateModel>>.Success(result);
    }

    public async Task<ApiResult> CloseConnection(string server, string target, string sessionId)
    {
        var result = await MikrotikHdl.CloseConnection(server, target, sessionId);

        if (!result)
        {
            return new ApiResult
            {
                Code = 500,
                Message = "بستن کانکشن با خطا مواجه شد!",
            };
        }

        return ApiResult.Success("کانکشن بسته شد.");
    }
}
