using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Mikrotik.Helper;

public static class BuiltInProcess
{
    public static ProcessEntity PppActiveRemoveBySession { get; } = new()
    {
        Name = "Close PPP Session",
        OsType = OsType.Mikrotik,
        Enable =
        [
            new ScriptEntity
            {
                Content = "/ppp active remove [find session-id=0x<session-id>]",
            },
            new ScriptEntity
            {
                Content = "/ppp active print where session-id=0x<session-id>",
                OutputPattern = "return",
            },
        ],
    };
    
    public static ProcessEntity PppActiveRemoveByUsername { get; } = new()
    {
        Name = "Close PPP User",
        OsType = OsType.Mikrotik,
        Enable =
        [
            new ScriptEntity
            {
                Content = "/ppp active remove [find name=<username>]",
            },
            new ScriptEntity
            {
                Content = "/ppp active print where name=<username>",
                OutputPattern = "return",
            },
        ],
    };
}
