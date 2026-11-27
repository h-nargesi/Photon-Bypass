using System.Text.RegularExpressions;

namespace PhotonBypass.ServerBridge.Ssh;

/// <summary>
/// RouterOS reports a failed command on stdout and often without an exit
/// status (Docs/25 §4.4): a line starting with one of these turns the command
/// into a failure instead of a silent success. The anchors keep print output
/// whose values merely contain the same words out.
/// </summary>
internal static class RouterOsErrorPatterns
{
    private const RegexOptions Style = RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase;

    private static readonly Regex[] Patterns =
    [
        new(@"^failure: ", Style),
        new(@"^expected end of command", Style),
        new(@"^not enough permissions", Style),
        new(@"^no such item", Style),
        new(@"^bad command name", Style),
        new(@"^syntax error", Style),
    ];

    public static bool IsAny(string output) => Patterns.Any(pattern => pattern.IsMatch(output));
}
