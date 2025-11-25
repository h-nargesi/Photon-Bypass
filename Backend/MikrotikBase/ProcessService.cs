
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Domain.Servers.Types;
using Renci.SshNet;

namespace PhotonBypass.Mikrotik.Helper;

public static class ProcessExtensions
{
    public static async Task<bool> ActiveOn(this ProcessEntity process, NasEntity server, ProcessContext context)
    {
        if (server.OsType != process.OsType)
            throw new Exception($"Invalid OS process ({process.OsType}) for server: {server.Name}");

        using var node = await server.Connect();
        return process.ActiveOn(node, context);
    }

    public static async Task<bool> DeactiveOn(this ProcessEntity process, NasEntity server, ProcessContext context)
    {
        if (server.OsType != process.OsType)
            throw new Exception($"Invalid OS process ({process.OsType}) for server: {server.Name}");

        using var node = await server.Connect();
        return process.DeactiveOn(node, context);
    }

    public static async Task<bool> CheckOn(this ProcessEntity process, NasEntity server, ProcessContext context)
    {
        if (server.OsType != process.OsType)
            throw new Exception($"Invalid OS process ({process.OsType}) for server: {server.Name}");

        if (process.Check == null) return false;

        using var node = await server.Connect();
        return process.CheckOn(node, context);
    }

    public static bool ActiveOn(this ProcessEntity process, SshClient node, ProcessContext context)
    {
        return node.Run(process.Enable, context);
    }

    public static bool DeactiveOn(this ProcessEntity process, SshClient node, ProcessContext context)
    {
        return node.Run(process.Disable, context);
    }

    public static bool CheckOn(this ProcessEntity process, SshClient node, ProcessContext context)
    {
        return process.Check != null && node.Run(process.Check, context);
    }

    private static bool Run(this SshClient node, IEnumerable<ScriptEntity> scripts, ProcessContext context)
    {
        return scripts.All(script => node.Run(script, context));
    }

    private static bool Run(this SshClient node, ScriptEntity script, ProcessContext context)
    {
        if (!node.Execute(script.Content, out var script_result))
        {
            context.Error = script_result;
            return false;
        }
        
        if (script.OutputPattern == "return")
        {
            context.Content.Add(script_result);
        }
        else if (!string.IsNullOrEmpty(script.OutputPattern))
        {
            // TODO: read output from script-result;
        }

        return true;
    }
}

public class ProcessContext : Dictionary<string, string>
{
    public string Error { get; set; }

    public List<string> Content { get; set; } = [];

    public string Result => string.Join('\n', Content);
}