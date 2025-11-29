using System.Text.RegularExpressions;

namespace PhotonBypass.ServerBridge.Ssh;

internal static partial class ScriptPatterns
{
    [GeneratedRegex(@"^#\s*\[Process\s*:\s*([\w \-]+)\]")]
    public static partial Regex ProcessParser();

    [GeneratedRegex(@"^#\s*\[Script\s*:\s*([\w \-])\s*:\s*(enable|disable|check)\]")]
    public static partial Regex ScriptParser();

    [GeneratedRegex(@"^#\s*\[Output\s*:\s*(result:regex)\]")]
    public static partial Regex OutputParser();

    [GeneratedRegex(@"(?m)^\s*#.*$", RegexOptions.Multiline)]
    public static partial Regex CommentRemover();

    [GeneratedRegex(@"#\s+regex:")]
    public static partial Regex OutputPatternParse();
}