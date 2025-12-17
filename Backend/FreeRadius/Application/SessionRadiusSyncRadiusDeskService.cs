using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.FreeRadius.WebService;
using PhotonBypass.Infra.Radius.RadiusDesk;

namespace PhotonBypass.FreeRadius.Application;

public class SessionRadiusSyncRadiusDeskService(RadWebApiOptionContext web_web_api_context) : ISessionRadiusSyncRadiusDeskService
{
    public Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username)
    {
        web_web_api_context.WebApiConfig = radius.Config?.WebApiConfig ??
                                           throw new Exception($"The web-config is not set for server ({radius.Id}:{radius.Name})");

        throw new NotImplementedException();
    }

    public Task CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id)
    {
        throw new NotImplementedException();
    }

    public Task CloseConnectionByUsername(ServerEntity radius, string username)
    {
        throw new NotImplementedException();
    }

    public Task<List<TrafficDataBinding>> GetTrafficData(ServerEntity radius, DateTime? index)
    {
        throw new NotImplementedException();
    }
}