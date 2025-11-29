using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Ssh;

namespace PhotonBypass.Mikrotik.Radius.Scripts;

public static class BuiltInProcess
{
    static BuiltInProcess()
    {
        var close_ppp_session_task = ProcessParser.ParseMikrotikScriptFile("close_ppp_session.rsc");
        var close_ppp_user_task = ProcessParser.ParseMikrotikScriptFile("close_ppp_user.rsc");

        Task.WaitAll(close_ppp_session_task, close_ppp_user_task);
        
        PppActiveRemoveBySession = close_ppp_session_task.Result ??
                                   throw new NullReferenceException("Cannot load script: close_ppp_session.rsc");
        
        PppActiveRemoveByUsername = close_ppp_user_task.Result ??
                                    throw new NullReferenceException("Cannot load script: close_ppp_user.rsc");
    }
    
    public static ProcessEntity PppActiveRemoveBySession { get; }
    
    public static ProcessEntity PppActiveRemoveByUsername { get; }
}
