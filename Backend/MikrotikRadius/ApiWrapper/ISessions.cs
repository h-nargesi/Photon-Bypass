using PhotonBypass.Mikrotik.Radius.Model;
using Refit;

namespace PhotonBypass.Mikrotik.Radius.ApiWrapper;

public interface ISessions
{
    public const string BasePath = "/user-manager/session";
    
    [Get("/print")]
    Task<List<SessionModel>> PrintBySessionId([AliasAs("acct-session-id")] string session_id);

    [Get("/print")]
    Task<List<SessionModel>> PrintByUsername([AliasAs("user")] string username, string? ended = null);
    
    [Get("/close-session")]
    Task<List<SessionModel>> CloseBySessionId([AliasAs("acct-session-id")] string session_id);
}