using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Mikrotik.Base;

public static class ProcessBusiness
{
    public static async Task<ProcessEntity?> ParseMikrotikScriptFile(string filename)
    {
        return ParseMikrotikScript(await File.ReadAllTextAsync(filename));
    }
    
    public static ProcessEntity? ParseMikrotikScript(string content)
    {
        var title_match = ScriptPatterns.ProcessParser().Match(content);
        if (!title_match.Success) return null;

        var process = new ProcessEntity
        {
            Name = title_match.Groups[1].Value,
            Enable = [],
            Disable = [],
            Check = new ScriptEntity(),
            OsType = OperatingSystem.Mikrotik,
        };

        var parts = ScriptPatterns.ScriptParser().Split(content);

        var order = 0;
        for (var i = 1; i < parts.Length; i += 3)
        {
            var name = parts[i];
            var type = Enum.Parse<ScriptType>(parts[i + 1], true);
            var body = parts[i + 2];

            var output_match = ScriptPatterns.OutputParser().Split(body);
            var output_pattern = output_match[1].ToLower();
            if (output_pattern == "regex")
            {
                output_pattern = output_match[2];
            }

            body = ScriptPatterns.CommentRemover().Replace(output_match[0], "");
            var script = new ScriptEntity
            {
                Type = type,
                Name = name,
                Content = body,
                OutputPattern = output_pattern,
                Order = ++order,
            };

            switch (type)
            {
                case ScriptType.Enable:
                    process.Enable.Add(script);
                    break;
                case ScriptType.Disable:
                    process.Disable.Add(script);
                    break;
                case ScriptType.Check:
                    process.Check = script;
                    break;
                default:
                    throw new Exception("Unknown script type");
            }
        }

        return process;
    }
}