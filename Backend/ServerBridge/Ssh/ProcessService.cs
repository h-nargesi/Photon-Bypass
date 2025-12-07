using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using Renci.SshNet;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotonBypass.ServerBridge.Ssh;

partial class ProcessService(ISshHandler handler) : IProcessService
{
    public async Task<bool> ActivateOn(ProcessEntity process, ServerEntity server, ProcessContext context)
    {
        if (server.OsType != process.OsType)
            throw new Exception($"Invalid OS process ({process.OsType}) for server: {server.Name}");

        using var node = await handler.ConnectTo(server);
        return ActivateOn(process, node, context);
    }

    public async Task<bool> DeactivateOn(ProcessEntity process, ServerEntity server, ProcessContext context)
    {
        if (server.OsType != process.OsType)
            throw new Exception($"Invalid OS process ({process.OsType}) for server: {server.Name}");

        using var node = await handler.ConnectTo(server);
        return DeactivateOn(process, node, context);
    }

    public async Task<bool> CheckOn(ProcessEntity process, ServerEntity server, ProcessContext context)
    {
        if (server.OsType != process.OsType)
            throw new Exception($"Invalid OS process ({process.OsType}) for server: {server.Name}");

        if (process.Check == null) return false;

        using var node = await handler.ConnectTo(server);
        return CheckOn(process, node, context);
    }

    private static bool ActivateOn(ProcessEntity process, SshClient node, ProcessContext context)
    {
        return process.Enable != null && Run(node, process.Enable, context);
    }

    private static bool DeactivateOn(ProcessEntity process, SshClient node, ProcessContext context)
    {
        return process.Disable != null && Run(node, process.Disable, context);
    }

    private static bool CheckOn(ProcessEntity process, SshClient node, ProcessContext context)
    {
        return process.Check != null && Run(node, process.Check, context);
    }

    private static bool Run(SshClient node, IEnumerable<ScriptEntity> scripts, ProcessContext context)
    {
        return scripts.All(script => Run(node, script, context));
    }

    private static bool Run(SshClient node, ScriptEntity script, ProcessContext context)
    {
        if (!node.Execute(InjectContextInScriptCommand(script, context), out var script_result))
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
            ReadScriptResult(script_result, script.OutputPattern, context);
        }

        return true;
    }

    private static string InjectContextInScriptCommand(ScriptEntity script, ProcessContext context)
    {
        if (context.Count < 1) return script.Content;

        var content = new StringBuilder(script.Content);

        foreach (var replace_pair in context)
        {
            if (!ValidCharacters().Match(replace_pair.Value).Success)
            {
                throw new Exception($"Invalid character for inject: {replace_pair.Key}={replace_pair.Value}");
            }
            
            content.Replace($"{{{replace_pair.Key}}}", replace_pair.Value);
        }

        return content.ToString();
    }

    private static void ReadScriptResult(string result, string output_pattern, ProcessContext context)
    {
        var patterns = ScriptPatterns.OutputPatternParse()
            .Split(output_pattern)
            .Select(pattern => pattern.Trim())
            .Where(pattern => pattern.Length > 0)
            .Select(pattern => pattern.Split('#'))
            .Where(pattern_info => pattern_info.Length > 1)
            .Select(pattern_info => new
            {
                Regex = new Regex(pattern_info[0].Trim(), RegexOptions.Compiled),
                Names = pattern_info.Skip(1)
                    .Where(replacement => replacement.Length > 0)
                    .Select(replacement => replacement.Split(':').Select(v => v.Trim()).ToArray())
                    .Where(replacement_info => replacement_info.Length == 2)
                    .Select(replacement_info => (Index: int.Parse(replacement_info[0]), Name: replacement_info[1]))
                    .GroupBy(k => k.Index)
                    .ToDictionary(k => k.Key, v=> v.Last().Name),
            })
            .ToArray();

        foreach (var pattern in patterns)
        {
            var match = pattern.Regex.Match(result);
            if (!match.Success) continue;
            foreach (var replacement_pair in pattern.Names.Where(replace_pair => match.Groups.Count > replace_pair.Key))
                context[replacement_pair.Value] = match.Groups[replacement_pair.Key].Value;
        }
    }

    [GeneratedRegex(@"^[ \-\.\w\d]+$", RegexOptions.Singleline)]
    private static partial Regex ValidCharacters();
}

public class ProcessContext : Dictionary<string, string>
{
    public string? Error { get; set; }

    public List<string> Content { get; set; } = [];

    public string Result => string.Join('\n', Content);
}