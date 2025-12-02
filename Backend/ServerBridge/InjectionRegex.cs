using System.Text.RegularExpressions;

namespace PhotonBypass.ServerBridge;

public static partial class InjectionRegex
{
    [GeneratedRegex("^[0-9a-fA-F]+$", RegexOptions.Singleline)]
    public static partial Regex SessionId();

    [GeneratedRegex(@"^[\d\w\-\.]+$", RegexOptions.Singleline)]
    public static partial Regex Username();
}
