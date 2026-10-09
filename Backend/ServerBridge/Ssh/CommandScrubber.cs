using System.Text.RegularExpressions;

namespace PhotonBypass.ServerBridge.Ssh;

public static partial class CommandScrubber
{
    public static string Scrub(string command)
    {
        return SecretPattern().Replace(command, "$1=\"***\"");
    }

    [GeneratedRegex("(?i)((?:export-)?pass(?:word|phrase)?)=\"[^\"]*\"")]
    private static partial Regex SecretPattern();
}
